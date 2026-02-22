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
public sealed class TilePanelNode : CompositeNode
{
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
    public ITileDataSource? DataSource { get; set; }

    /// <summary>
    /// Points of interest to display on the map.
    /// </summary>
    public IReadOnlyList<TilePointOfInterest> PointsOfInterest { get; set; } = [];

    /// <summary>
    /// Pan callback, invoked by input bindings with (deltaX, deltaY, context).
    /// </summary>
    public Func<double, double, InputBindingActionContext, Task>? PanCallback { get; set; }

    /// <summary>
    /// Zoom callback, invoked by input bindings with (delta, context).
    /// </summary>
    public Func<int, InputBindingActionContext, Task>? ZoomCallback { get; set; }

    /// <summary>
    /// POI click callback.
    /// </summary>
    public Func<TilePointOfInterest, InputBindingActionContext, Task>? PoiClickedCallback { get; set; }

    // Cached tile data from last fetch
    private TileData[,]? _cachedTiles;
    private int _cachedTileX;
    private int _cachedTileY;
    private int _cachedTilesWide;
    private int _cachedTilesTall;

    /// <summary>
    /// Gets the effective tile render width at the current zoom level.
    /// </summary>
    public int EffectiveTileWidth => DataSource != null
        ? Math.Max(1, (int)(DataSource.TileSize.Width * Math.Pow(2, Math.Clamp(ZoomLevel, -4, 8))))
        : 1;

    /// <summary>
    /// Gets the effective tile render height at the current zoom level.
    /// </summary>
    public int EffectiveTileHeight => DataSource != null
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
        var poiLayer = BuildPoiFloatPanel();

        // Wrap the tile surface in an Interactable so it receives focus and input
        Hex1bWidget interactableTiles = new InteractableWidget(_ => tileLayer)
            .WithInputBindings(ConfigureBindings);

        // ZStack: tiles at bottom, POIs on top
        if (poiLayer != null)
        {
            return new ZStackWidget([interactableTiles, poiLayer]);
        }

        return interactableTiles;
    }

    private SurfaceWidget BuildTileSurfaceWidget()
    {
        return new SurfaceWidget(ctx =>
        {
            return [ctx.Layer(surface => DrawTiles(surface))];
        });
    }

    private void DrawTiles(Surface surface)
    {
        if (DataSource == null) return;

        var tileW = EffectiveTileWidth;
        var tileH = EffectiveTileHeight;
        if (tileW <= 0 || tileH <= 0) return;

        // Use the surface dimensions for viewport calculations (not Bounds,
        // which may not be set when the DrawSurfaceLayer callback fires)
        var viewWidth = surface.Width;
        var viewHeight = surface.Height;
        if (viewWidth <= 0 || viewHeight <= 0) return;

        var startTileX = (int)Math.Floor(CameraX - (double)viewWidth / (2 * tileW));
        var startTileY = (int)Math.Floor(CameraY - (double)viewHeight / (2 * tileH));
        var tilesWide = viewWidth / tileW + 2;
        var tilesTall = viewHeight / tileH + 2;

        // Use cached tiles if available and covering the same region
        var tiles = _cachedTiles;
        if (tiles == null
            || _cachedTileX != startTileX || _cachedTileY != startTileY
            || _cachedTilesWide != tilesWide || _cachedTilesTall != tilesTall)
        {
            // Synchronously wait — tile data sources should be fast for visible regions
            tiles = DataSource.GetTilesAsync(startTileX, startTileY, tilesWide, tilesTall)
                .AsTask().GetAwaiter().GetResult();
            _cachedTiles = tiles;
            _cachedTileX = startTileX;
            _cachedTileY = startTileY;
            _cachedTilesWide = tilesWide;
            _cachedTilesTall = tilesTall;
        }

        for (int ty = 0; ty < tilesTall && ty < tiles.GetLength(1); ty++)
        {
            for (int tx = 0; tx < tilesWide && tx < tiles.GetLength(0); tx++)
            {
                var tile = tiles[tx, ty];
                // Compute screen position using surface dimensions
                var screenX = (int)(((startTileX + tx) - CameraX) * tileW + viewWidth / 2.0);
                var screenY = (int)(((startTileY + ty) - CameraY) * tileH + viewHeight / 2.0);

                if (string.IsNullOrEmpty(tile.Content))
                    continue;

                // Render tile content at screen position
                // For zoomed tiles, repeat/scale the content
                for (int row = 0; row < tileH; row++)
                {
                    var contentRow = tileH > 1 && DataSource.TileSize.Height > 1
                        ? row * DataSource.TileSize.Height / tileH
                        : 0;

                    // Scale content horizontally
                    var content = ScaleTileContent(tile.Content, tileW, DataSource.TileSize.Width);

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

    private FloatPanelWidget? BuildPoiFloatPanel()
    {
        if (PointsOfInterest.Count == 0)
            return null;

        var children = new List<FloatChild>();

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

            children.Add(new FloatChild(screenX, screenY, poiWidget));
        }

        return children.Count > 0 ? new FloatPanelWidget(children) : null;
    }

    /// <summary>
    /// Configures the input bindings applied to the Interactable wrapping the tile surface.
    /// </summary>
    private void ConfigureBindings(InputBindingsBuilder bindings)
    {
        // Pan by 1 tile
        bindings.Key(Hex1bKey.UpArrow).Action(ctx => HandlePan(ctx, 0, -1), "Pan up");
        bindings.Key(Hex1bKey.DownArrow).Action(ctx => HandlePan(ctx, 0, 1), "Pan down");
        bindings.Key(Hex1bKey.LeftArrow).Action(ctx => HandlePan(ctx, -1, 0), "Pan left");
        bindings.Key(Hex1bKey.RightArrow).Action(ctx => HandlePan(ctx, 1, 0), "Pan right");

        // Pan by 5 tiles
        bindings.Shift().Key(Hex1bKey.UpArrow).Action(ctx => HandlePan(ctx, 0, -5), "Pan up fast");
        bindings.Shift().Key(Hex1bKey.DownArrow).Action(ctx => HandlePan(ctx, 0, 5), "Pan down fast");
        bindings.Shift().Key(Hex1bKey.LeftArrow).Action(ctx => HandlePan(ctx, -5, 0), "Pan left fast");
        bindings.Shift().Key(Hex1bKey.RightArrow).Action(ctx => HandlePan(ctx, 5, 0), "Pan right fast");

        // Zoom — support both numpad (+/-) and regular keyboard (=/-)
        bindings.Key(Hex1bKey.Add).Action(ctx => HandleZoom(ctx, 1), "Zoom in");
        bindings.Key(Hex1bKey.Subtract).Action(ctx => HandleZoom(ctx, -1), "Zoom out");
        bindings.Key(Hex1bKey.OemPlus).Action(ctx => HandleZoom(ctx, 1), "Zoom in");
        bindings.Key(Hex1bKey.OemMinus).Action(ctx => HandleZoom(ctx, -1), "Zoom out");

        // Mouse scroll for zoom
        bindings.Mouse(MouseButton.ScrollUp).Action(ctx => HandleZoom(ctx, 1), "Zoom in");
        bindings.Mouse(MouseButton.ScrollDown).Action(ctx => HandleZoom(ctx, -1), "Zoom out");

        // Mouse drag for panning
        bindings.Drag(MouseButton.Left).Action(HandleDragStart, "Pan by dragging");

        // Reset to origin
        bindings.Key(Hex1bKey.Home).Action(ctx => HandlePan(ctx, -CameraX, -CameraY), "Reset position");
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
            return ZoomCallback(delta, ctx);
        }
        return Task.CompletedTask;
    }
}
