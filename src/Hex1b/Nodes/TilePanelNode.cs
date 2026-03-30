using Hex1b.Data;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="TilePanelWidget"/>.
/// Manages viewport math, tile fetching, POI positioning, and input bindings.
/// </summary>
public sealed class TilePanelNode : Hex1bNode, IDisposable
{
    /// <summary>
    /// The reconciled content child node.
    /// This is the root of the widget tree built by the tile panel widget.
    /// </summary>
    public Hex1bNode? ContentChild { get; set; }

    /// <summary>
    /// Reference to the source widget for typed event args.
    /// </summary>
    internal Hex1bWidget? SourceWidget { get; set; }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ContentChild != null) yield return ContentChild;
    }

    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
        => ContentChild?.Measure(constraints) ?? constraints.Constrain(Size.Zero);

    /// <inheritdoc />
    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        ContentChild?.Arrange(rect);
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (ContentChild != null)
            context.RenderChild(ContentChild);
    }

    /// <summary>
    /// Tile panel nodes are NOT themselves focusable - focus passes through to children.
    /// </summary>
    public override bool IsFocusable => false;

    /// <summary>
    /// Focus state exists on the content child, not the tile panel node itself.
    /// </summary>
    public override bool IsFocused
    {
        get => false;
        set
        {
            if (ContentChild != null)
                ContentChild.IsFocused = value;
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (ContentChild != null)
        {
            foreach (var focusable in ContentChild.GetFocusableNodes())
                yield return focusable;
        }
    }

    /// <summary>
    /// Camera X position in tile coordinates.
    /// </summary>
    public double CameraX { get; set; }

    /// <summary>
    /// Camera Y position in tile coordinates.
    /// </summary>
    public double CameraY { get; set; }

    /// <summary>
    /// Zoom level. 0 = 1x, 1 = 2x, 2 = 4x, etc.
    /// </summary>
    public int ZoomLevel { get; set; }

    /// <summary>
    /// The tile data source.
    /// </summary>
    public ITileDataSource? DataSource { get; private set; }

    /// <summary>
    /// Points of interest to display on the map.
    /// </summary>
    public IReadOnlyList<TilePointOfInterest> PointsOfInterest { get; internal set; } = [];

    /// <summary>
    /// Pan callback, invoked by input bindings with (deltaX, deltaY, context).
    /// </summary>
    internal Func<double, double, InputBindingActionContext, Task>? PanCallback { get; set; }

    /// <summary>
    /// Zoom callback, invoked by input bindings with (delta, pivotX, pivotY, context).
    /// </summary>
    internal Func<int, double, double, InputBindingActionContext, Task>? ZoomCallback { get; set; }

    /// <summary>
    /// POI click callback.
    /// </summary>
    internal Func<TilePointOfInterest, InputBindingActionContext, Task>? PoiClickedCallback { get; set; }

    // Tile caching via TileCache (never blocks render thread)
    private TileCache? _tileCache;
    private ITileDataSource? _currentDataSource;

    // Invalidation callback from ReconcileContext
    private Action? _invalidateCallback;

    // Empty tile placeholder styling (matching TilePanelTheme defaults)
    internal char EmptyTileCharacter { get; set; } = '·';
    internal Hex1bColor? EmptyTileForeground { get; set; } = Hex1bColor.DarkGray;
    internal Hex1bColor? EmptyTileBackground { get; set; }

    /// <summary>
    /// Sets the invalidation callback used to trigger app re-renders
    /// when background-fetched tiles become available.
    /// </summary>
    internal void SetInvalidateCallback(Action callback)
    {
        _invalidateCallback = callback;
    }

    /// <summary>
    /// Sets the data source. Creates or recreates the internal sync/async bridge
    /// when the data source changes.
    /// </summary>
    internal void SetDataSource(ITileDataSource dataSource)
    {
        DataSource = dataSource;

        if (!ReferenceEquals(dataSource, _currentDataSource))
        {
            // Data source changed — recreate cache
            if (_tileCache != null)
            {
                _tileCache.TilesAvailable -= OnTilesAvailable;
                _tileCache.Dispose();
            }

            _currentDataSource = dataSource;
            _tileCache = new TileCache(dataSource);
            _tileCache.TilesAvailable += OnTilesAvailable;
        }
    }

    private void OnTilesAvailable()
    {
        // Don't call MarkDirty() here — we're on a ThreadPool thread and
        // MarkDirty isn't thread-safe (races with render loop's version checks).
        // Just trigger a new render cycle. SurfaceWidget.ReconcileExistingNode
        // always marks the SurfaceNode dirty during reconciliation, so the
        // DrawTiles callback will re-run and pick up the newly cached tiles.
        _invalidateCallback?.Invoke();
    }

    /// <summary>
    /// Gets the effective tile render width at the current zoom level.
    /// </summary>
    internal int EffectiveTileWidth => DataSource != null
        ? Math.Max(1, (int)(DataSource.TileSize.Width * Math.Pow(2, Math.Clamp(ZoomLevel, -4, 8))))
        : 1;

    /// <summary>
    /// Gets the effective tile render height at the current zoom level.
    /// </summary>
    internal int EffectiveTileHeight => DataSource != null
        ? Math.Max(1, (int)(DataSource.TileSize.Height * Math.Pow(2, Math.Clamp(ZoomLevel, -4, 8))))
        : 1;

    /// <summary>
    /// Calculates the visible tile range for the current viewport and camera.
    /// </summary>
    internal (int tileX, int tileY, int tilesWide, int tilesTall) GetVisibleTileRange()
    {
        var viewWidth = Bounds.Width;
        var viewHeight = Bounds.Height;
        var tileW = EffectiveTileWidth;
        var tileH = EffectiveTileHeight;

        if (tileW <= 0 || tileH <= 0 || viewWidth <= 0 || viewHeight <= 0)
            return (0, 0, 0, 0);

        // Camera is at center of viewport
        var startTileX = (int)Math.Floor(CameraX - (double)viewWidth / (2 * tileW));
        var startTileY = (int)Math.Floor(CameraY - (double)viewHeight / (2 * tileH));
        // +2 for partial tiles at both edges
        var tilesWide = viewWidth / tileW + 2;
        var tilesTall = viewHeight / tileH + 2;

        return (startTileX, startTileY, tilesWide, tilesTall);
    }

    /// <summary>
    /// Converts tile coordinates to screen coordinates (relative to the panel's bounds).
    /// </summary>
    internal (int screenX, int screenY) TileToScreen(double tileX, double tileY)
    {
        var tileW = EffectiveTileWidth;
        var tileH = EffectiveTileHeight;
        var viewWidth = Bounds.Width;
        var viewHeight = Bounds.Height;

        // Camera is at center of viewport
        var screenX = (int)((tileX - CameraX) * tileW + viewWidth / 2.0);
        var screenY = (int)((tileY - CameraY) * tileH + viewHeight / 2.0);

        return (screenX, screenY);
    }

    /// <summary>
    /// Builds the composed widget content for this tile panel.
    /// The SurfaceWidget is wrapped in an Interactable to get proper focus,
    /// hit-testing, and input binding support.
    /// </summary>
    internal Hex1bWidget BuildContent()
    {
        var tileLayer = BuildTileSurfaceWidget();
        var poiFloats = BuildPoiFloats();

        // Wrap the tile surface in an Interactable so it receives focus and input
        Hex1bWidget interactableTiles = new InteractableWidget(_ => tileLayer)
            .WithInputBindings(ConfigureBindings);

        // ZStack: tiles at bottom, POI floats on top
        if (poiFloats.Count > 0)
        {
            var children = new List<Hex1bWidget> { interactableTiles };
            children.AddRange(poiFloats);
            return new ZStackWidget(children);
        }

        return interactableTiles;
    }

    private Hex1bWidget BuildTileSurfaceWidget()
    {
        return new SurfaceWidget(ctx =>
        {
            return [ctx.Layer(surface => DrawTiles(surface))];
        });
    }

    private void DrawTiles(Surface surface)
    {
        if (_tileCache == null) return;

        var tileW = EffectiveTileWidth;
        var tileH = EffectiveTileHeight;
        if (tileW <= 0 || tileH <= 0) return;

        var viewWidth = surface.Width;
        var viewHeight = surface.Height;
        if (viewWidth <= 0 || viewHeight <= 0) return;

        var startTileX = (int)Math.Floor(CameraX - (double)viewWidth / (2 * tileW));
        var startTileY = (int)Math.Floor(CameraY - (double)viewHeight / (2 * tileH));
        var tilesWide = viewWidth / tileW + 2;
        var tilesTall = viewHeight / tileH + 2;

        // Get tiles from cache — never blocks. Uncached tiles are default(TileData)
        // and will be fetched asynchronously in the background.
        var tiles = _tileCache.GetTiles(startTileX, startTileY, tilesWide, tilesTall);

        for (int ty = 0; ty < tilesTall && ty < tiles.GetLength(1); ty++)
        {
            for (int tx = 0; tx < tilesWide && tx < tiles.GetLength(0); tx++)
            {
                var tile = tiles[tx, ty];
                var screenX = (int)(((startTileX + tx) - CameraX) * tileW + viewWidth / 2.0);
                var screenY = (int)(((startTileY + ty) - CameraY) * tileH + viewHeight / 2.0);

                if (string.IsNullOrEmpty(tile.Content))
                {
                    // Render placeholder for uncached tiles
                    var placeholder = new string(EmptyTileCharacter, tileW);
                    for (int row = 0; row < tileH; row++)
                    {
                        surface.WriteText(
                            screenX, screenY + row,
                            placeholder,
                            EmptyTileForeground,
                            EmptyTileBackground);
                    }
                    continue;
                }

                var tileSize = _tileCache.TileSize;
                for (int row = 0; row < tileH; row++)
                {
                    var contentRow = tileH > 1 && tileSize.Height > 1
                        ? row * tileSize.Height / tileH
                        : 0;

                    var content = ScaleTileContent(tile.Content, tileW, tileSize.Width);

                    surface.WriteText(
                        screenX, screenY + row,
                        content,
                        tile.Foreground.IsDefault ? null : tile.Foreground,
                        tile.Background.IsDefault ? null : tile.Background);
                }
            }
        }
    }

    private static string ScaleTileContent(string content, int targetWidth, int sourceWidth)
    {
        if (targetWidth <= 0) return "";
        if (targetWidth == sourceWidth) return content;

        // Simple scaling: repeat or truncate characters
        var result = new char[targetWidth];
        for (int i = 0; i < targetWidth; i++)
        {
            var sourceIdx = sourceWidth > 0 ? i * sourceWidth / targetWidth : 0;
            result[i] = sourceIdx < content.Length ? content[sourceIdx] : ' ';
        }
        return new string(result);
    }

    private List<FloatWidget> BuildPoiFloats()
    {
        var floats = new List<FloatWidget>();

        if (PointsOfInterest.Count == 0)
            return floats;

        foreach (var poi in PointsOfInterest)
        {
            var (screenX, screenY) = TileToScreen(poi.X, poi.Y);

            // Skip POIs outside the viewport
            if (screenX < -10 || screenX >= Bounds.Width + 10
                || screenY < -5 || screenY >= Bounds.Height + 5)
                continue;

            Hex1bWidget poiWidget;
            if (PoiClickedCallback != null)
            {
                var capturedPoi = poi;
                poiWidget = new IconWidget(poi.Icon)
                    .OnClick(e =>
                    {
                        PoiClickedCallback(capturedPoi, e.Context).GetAwaiter().GetResult();
                    });
            }
            else
            {
                poiWidget = new IconWidget(poi.Icon);
            }

            floats.Add(new FloatWidget(poiWidget).Absolute(screenX, screenY));
        }

        return floats;
    }

    /// <summary>
    /// Configures the input bindings applied to the Interactable wrapping the tile surface.
    /// </summary>
    private void ConfigureBindings(InputBindingsBuilder bindings)
    {
        // Pan by 1 tile
        bindings.Key(Hex1bKey.UpArrow).Triggers(TilePanelWidget.PanUp, ctx => HandlePan(ctx, 0, -1), "Pan up");
        bindings.Key(Hex1bKey.DownArrow).Triggers(TilePanelWidget.PanDown, ctx => HandlePan(ctx, 0, 1), "Pan down");
        bindings.Key(Hex1bKey.LeftArrow).Triggers(TilePanelWidget.PanLeft, ctx => HandlePan(ctx, -1, 0), "Pan left");
        bindings.Key(Hex1bKey.RightArrow).Triggers(TilePanelWidget.PanRight, ctx => HandlePan(ctx, 1, 0), "Pan right");

        // Pan by 5 tiles
        bindings.Shift().Key(Hex1bKey.UpArrow).Triggers(TilePanelWidget.PanUpFast, ctx => HandlePan(ctx, 0, -5), "Pan up fast");
        bindings.Shift().Key(Hex1bKey.DownArrow).Triggers(TilePanelWidget.PanDownFast, ctx => HandlePan(ctx, 0, 5), "Pan down fast");
        bindings.Shift().Key(Hex1bKey.LeftArrow).Triggers(TilePanelWidget.PanLeftFast, ctx => HandlePan(ctx, -5, 0), "Pan left fast");
        bindings.Shift().Key(Hex1bKey.RightArrow).Triggers(TilePanelWidget.PanRightFast, ctx => HandlePan(ctx, 5, 0), "Pan right fast");

        // Zoom — support both numpad (+/-) and regular keyboard (=/-)
        bindings.Key(Hex1bKey.Add).Triggers(TilePanelWidget.ZoomIn, ctx => HandleZoom(ctx, 1), "Zoom in");
        bindings.Key(Hex1bKey.Subtract).Triggers(TilePanelWidget.ZoomOut, ctx => HandleZoom(ctx, -1), "Zoom out");
        bindings.Key(Hex1bKey.OemPlus).Triggers(TilePanelWidget.ZoomIn);
        bindings.Key(Hex1bKey.OemMinus).Triggers(TilePanelWidget.ZoomOut);

        // Mouse scroll for zoom
        bindings.Mouse(MouseButton.ScrollUp).Triggers(TilePanelWidget.ZoomIn, ctx => HandleZoom(ctx, 1), "Zoom in");
        bindings.Mouse(MouseButton.ScrollDown).Triggers(TilePanelWidget.ZoomOut, ctx => HandleZoom(ctx, -1), "Zoom out");

        // Mouse drag for panning
        bindings.Drag(MouseButton.Left).Action(HandleDragStart, "Pan by dragging");

        // Reset to origin
        bindings.Key(Hex1bKey.Home).Triggers(TilePanelWidget.ResetPosition, ctx => HandlePan(ctx, -CameraX, -CameraY), "Reset position");
    }

    // Track cumulative drag delta to convert absolute deltas to incremental ones
    private int _lastDragDeltaX;
    private int _lastDragDeltaY;

    private DragHandler HandleDragStart(int localX, int localY)
    {
        _lastDragDeltaX = 0;
        _lastDragDeltaY = 0;

        return new DragHandler(
            onMove: (ctx, deltaX, deltaY) =>
            {
                // deltaX/deltaY are cumulative from drag start — compute incremental
                var incrX = deltaX - _lastDragDeltaX;
                var incrY = deltaY - _lastDragDeltaY;
                _lastDragDeltaX = deltaX;
                _lastDragDeltaY = deltaY;

                if (incrX == 0 && incrY == 0) return;

                var tileW = EffectiveTileWidth;
                var tileH = EffectiveTileHeight;

                // Drag moves the map opposite to cursor direction
                var tileDx = -(double)incrX / tileW;
                var tileDy = -(double)incrY / tileH;

                HandlePan(ctx, tileDx, tileDy).GetAwaiter().GetResult();
            });
    }

    private Task HandlePan(InputBindingActionContext ctx, double dx, double dy)
    {
        if (PanCallback != null)
        {
            return PanCallback(dx, dy, ctx);
        }
        return Task.CompletedTask;
    }

    private Task HandleZoom(InputBindingActionContext ctx, int delta)
    {
        if (ZoomCallback != null)
        {
            // Compute pivot point in tile-space
            double pivotX = CameraX;
            double pivotY = CameraY;

            // If this is a mouse event, pivot at the cursor position
            if (ctx.MouseX >= 0 && ctx.MouseY >= 0 && Bounds.Width > 0 && Bounds.Height > 0)
            {
                var localX = ctx.MouseX - Bounds.X;
                var localY = ctx.MouseY - Bounds.Y;
                var tileW = EffectiveTileWidth;
                var tileH = EffectiveTileHeight;
                if (tileW > 0 && tileH > 0)
                {
                    pivotX = CameraX + (localX - Bounds.Width / 2.0) / tileW;
                    pivotY = CameraY + (localY - Bounds.Height / 2.0) / tileH;
                }
            }

            return ZoomCallback(delta, pivotX, pivotY, ctx);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the tile cache and cancels pending background fetches.
    /// </summary>
    public void Dispose()
    {
        if (_tileCache != null)
        {
            _tileCache.TilesAvailable -= OnTilesAvailable;
            _tileCache.Dispose();
            _tileCache = null;
        }
    }
}
