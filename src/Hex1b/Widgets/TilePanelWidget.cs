using Hex1b.Data;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A composite widget that renders an infinite, pannable and zoomable tile map.
/// </summary>
/// <remarks>
/// <para>
/// TilePanel composes a <see cref="SurfaceWidget"/> for tile rendering,
/// <see cref="FloatWidget"/> elements for positioning points of interest,
/// and a <see cref="ZStackWidget"/> for layering and overlay support.
/// </para>
/// <para>
/// Camera position and zoom are controlled externally by the user via
/// <see cref="OnPan(Action{TilePanelPanEventArgs})"/> and <see cref="OnZoom(Action{TilePanelZoomEventArgs})"/> event handlers. The user
/// updates their state in response to events, and the new position flows
/// back as widget properties — following the "controlled component" pattern.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var cameraX = 0.0;
/// var cameraY = 0.0;
/// var zoom = 0;
///
/// ctx.TilePanel(myDataSource, cameraX, cameraY, zoom)
///     .WithPointsOfInterest([new TilePointOfInterest(5, 3, "📍", "Spawn")])
///     .OnPan(e => { cameraX += e.DeltaX; cameraY += e.DeltaY; })
///     .OnZoom(e => zoom = e.NewZoomLevel)
/// </code>
/// </example>
public sealed record TilePanelWidget : CompositeWidget<TilePanelNode>
{
    /// <summary>
    /// The tile data source providing tile content.
    /// </summary>
    public required ITileDataSource DataSource { get; init; }

    /// <summary>Rebindable action: Pan camera up.</summary>
    public static readonly ActionId PanUp = new($"{nameof(TilePanelWidget)}.{nameof(PanUp)}");

    /// <summary>Rebindable action: Pan camera down.</summary>
    public static readonly ActionId PanDown = new($"{nameof(TilePanelWidget)}.{nameof(PanDown)}");

    /// <summary>Rebindable action: Pan camera left.</summary>
    public static readonly ActionId PanLeft = new($"{nameof(TilePanelWidget)}.{nameof(PanLeft)}");

    /// <summary>Rebindable action: Pan camera right.</summary>
    public static readonly ActionId PanRight = new($"{nameof(TilePanelWidget)}.{nameof(PanRight)}");

    /// <summary>Rebindable action: Pan camera up fast.</summary>
    public static readonly ActionId PanUpFast = new($"{nameof(TilePanelWidget)}.{nameof(PanUpFast)}");

    /// <summary>Rebindable action: Pan camera down fast.</summary>
    public static readonly ActionId PanDownFast = new($"{nameof(TilePanelWidget)}.{nameof(PanDownFast)}");

    /// <summary>Rebindable action: Pan camera left fast.</summary>
    public static readonly ActionId PanLeftFast = new($"{nameof(TilePanelWidget)}.{nameof(PanLeftFast)}");

    /// <summary>Rebindable action: Pan camera right fast.</summary>
    public static readonly ActionId PanRightFast = new($"{nameof(TilePanelWidget)}.{nameof(PanRightFast)}");

    /// <summary>Rebindable action: Zoom in.</summary>
    public static readonly ActionId ZoomIn = new($"{nameof(TilePanelWidget)}.{nameof(ZoomIn)}");

    /// <summary>Rebindable action: Zoom out.</summary>
    public static readonly ActionId ZoomOut = new($"{nameof(TilePanelWidget)}.{nameof(ZoomOut)}");

    /// <summary>Rebindable action: Reset camera to origin.</summary>
    public static readonly ActionId ResetPosition = new($"{nameof(TilePanelWidget)}.{nameof(ResetPosition)}");

    /// <summary>
    /// The camera X position in tile coordinates.
    /// </summary>
    public double CameraX { get; init; }

    /// <summary>
    /// The camera Y position in tile coordinates.
    /// </summary>
    public double CameraY { get; init; }

    /// <summary>
    /// The zoom level. 0 = 1x, 1 = 2x, 2 = 4x, -1 = 0.5x, etc.
    /// Each level doubles/halves the tile render size.
    /// </summary>
    public int ZoomLevel { get; init; }

    /// <summary>
    /// Points of interest to display on the map.
    /// </summary>
    public IReadOnlyList<TilePointOfInterest> PointsOfInterest { get; init; } = [];

    /// <summary>
    /// Pan event handler. Called with (deltaX, deltaY) when the user pans.
    /// </summary>
    internal Func<TilePanelPanEventArgs, Task>? PanHandler { get; init; }

    /// <summary>
    /// Zoom event handler. Called when the user zooms in or out.
    /// </summary>
    internal Func<TilePanelZoomEventArgs, Task>? ZoomHandler { get; init; }

    /// <summary>
    /// POI click event handler. Called when a point of interest is clicked.
    /// </summary>
    internal Func<TilePanelPoiClickedEventArgs, Task>? PoiClickedHandler { get; init; }

    /// <summary>
    /// Attaches a synchronous pan handler.
    /// </summary>
    public TilePanelWidget OnPan(Action<TilePanelPanEventArgs> handler)
        => this with { PanHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Attaches an asynchronous pan handler.
    /// </summary>
    public TilePanelWidget OnPan(Func<TilePanelPanEventArgs, Task> handler)
        => this with { PanHandler = handler };

    /// <summary>
    /// Attaches a synchronous zoom handler.
    /// </summary>
    public TilePanelWidget OnZoom(Action<TilePanelZoomEventArgs> handler)
        => this with { ZoomHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Attaches an asynchronous zoom handler.
    /// </summary>
    public TilePanelWidget OnZoom(Func<TilePanelZoomEventArgs, Task> handler)
        => this with { ZoomHandler = handler };

    /// <summary>
    /// Attaches a synchronous POI click handler.
    /// </summary>
    public TilePanelWidget OnPoiClicked(Action<TilePanelPoiClickedEventArgs> handler)
        => this with { PoiClickedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Attaches an asynchronous POI click handler.
    /// </summary>
    public TilePanelWidget OnPoiClicked(Func<TilePanelPoiClickedEventArgs, Task> handler)
        => this with { PoiClickedHandler = handler };

    /// <summary>
    /// Sets the points of interest to display on the map.
    /// </summary>
    public TilePanelWidget WithPointsOfInterest(IReadOnlyList<TilePointOfInterest> pois)
        => this with { PointsOfInterest = pois };

    protected override void UpdateNode(TilePanelNode node)
    {
        if (node.CameraX != CameraX || node.CameraY != CameraY || node.ZoomLevel != ZoomLevel)
        {
            node.MarkDirty();
        }

        node.CameraX = CameraX;
        node.CameraY = CameraY;
        node.ZoomLevel = ZoomLevel;
        node.SetDataSource(DataSource);
        node.PointsOfInterest = PointsOfInterest;

        // Wire up event handler callbacks
        if (PanHandler != null)
        {
            node.PanCallback = async (dx, dy, ctx) =>
            {
                var args = new TilePanelPanEventArgs(dx, dy, this, node, ctx);
                await PanHandler(args);
            };
        }
        else
        {
            node.PanCallback = null;
        }

        if (ZoomHandler != null)
        {
            node.ZoomCallback = async (delta, pivotX, pivotY, ctx) =>
            {
                var args = new TilePanelZoomEventArgs(node.ZoomLevel + delta, delta, pivotX, pivotY, this, node, ctx);
                await ZoomHandler(args);
            };
        }
        else
        {
            node.ZoomCallback = null;
        }

        if (PoiClickedHandler != null)
        {
            node.PoiClickedCallback = async (poi, ctx) =>
            {
                var args = new TilePanelPoiClickedEventArgs(poi, this, node, ctx);
                await PoiClickedHandler(args);
            };
        }
        else
        {
            node.PoiClickedCallback = null;
        }
    }

    protected override Task<Hex1bWidget> BuildContentAsync(TilePanelNode node, ReconcileContext context)
    {
        // Wire InvalidateCallback so TileCache can trigger re-renders
        if (context.InvalidateCallback != null)
        {
            node.SetInvalidateCallback(context.InvalidateCallback);
        }

        Hex1bWidget content = node.BuildContent();

        return Task.FromResult(content);
    }
}
