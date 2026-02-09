using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Context provided to <see cref="SurfaceWidget"/> layer builders.
/// </summary>
/// <remarks>
/// <para>
/// This context is passed to the layer builder callback and provides:
/// <list type="bullet">
///   <item>Factory methods for creating layers</item>
///   <item>Mouse position for interactive effects</item>
///   <item>Theme access for styled effects</item>
///   <item>Sixel creation for graphics</item>
/// </list>
/// </para>
/// <para>
/// The context is created fresh for each render, with current mouse
/// position and theme state.
/// </para>
/// </remarks>
public class SurfaceLayerContext
{
    private readonly TrackedObjectStore? _store;
    
    /// <summary>
    /// Gets the current mouse X position (column) relative to the widget.
    /// </summary>
    /// <remarks>
    /// Returns -1 if the mouse is not over the widget.
    /// </remarks>
    public int MouseX { get; }

    /// <summary>
    /// Gets the current mouse Y position (row) relative to the widget.
    /// </summary>
    /// <remarks>
    /// Returns -1 if the mouse is not over the widget.
    /// </remarks>
    public int MouseY { get; }

    /// <summary>
    /// Gets the current theme.
    /// </summary>
    public Hex1bTheme Theme { get; }

    /// <summary>
    /// Gets the width of the surface in columns.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the surface in rows.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the cell metrics (pixel dimensions per cell).
    /// </summary>
    /// <remarks>
    /// Used to correctly size sixel graphics to match cell dimensions.
    /// </remarks>
    public CellMetrics CellMetrics { get; }

    /// <summary>
    /// Creates a new SurfaceLayerContext.
    /// </summary>
    internal SurfaceLayerContext(int width, int height, int mouseX, int mouseY, Hex1bTheme theme, TrackedObjectStore? store = null, CellMetrics? cellMetrics = null)
    {
        Width = width;
        Height = height;
        MouseX = mouseX;
        MouseY = mouseY;
        Theme = theme;
        _store = store;
        CellMetrics = cellMetrics ?? CellMetrics.Default;
    }

    /// <summary>
    /// Creates a layer from an existing surface source.
    /// </summary>
    /// <param name="source">The surface source providing cell data.</param>
    /// <param name="offsetX">X offset where the source's (0,0) will be placed.</param>
    /// <param name="offsetY">Y offset where the source's (0,0) will be placed.</param>
    /// <returns>A layer that renders the source at the specified offset.</returns>
    public SurfaceLayer Layer(ISurfaceSource source, int offsetX = 0, int offsetY = 0)
        => new SourceSurfaceLayer(source, offsetX, offsetY);

    /// <summary>
    /// Creates a layer whose content is drawn via a callback.
    /// </summary>
    /// <param name="draw">The callback that draws content to a fresh surface.</param>
    /// <param name="offsetX">X offset where the drawn surface's (0,0) will be placed.</param>
    /// <param name="offsetY">Y offset where the drawn surface's (0,0) will be placed.</param>
    /// <returns>A layer that renders the drawn content at the specified offset.</returns>
    public SurfaceLayer Layer(Action<Surface> draw, int offsetX = 0, int offsetY = 0)
        => new DrawSurfaceLayer(draw, offsetX, offsetY);

    /// <summary>
    /// Creates a computed layer whose cells are calculated dynamically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The compute delegate is called for each cell during compositing.
    /// It receives a <see cref="ComputeContext"/> providing access to
    /// cells from layers below.
    /// </para>
    /// <para>
    /// Use this for effects like fog of war, color overlays, vignettes,
    /// or any effect that depends on the content of layers below.
    /// </para>
    /// </remarks>
    /// <param name="compute">The delegate that computes each cell's value.</param>
    /// <returns>A computed layer covering the entire surface.</returns>
    public SurfaceLayer Layer(CellCompute compute)
        => new ComputedSurfaceLayer(compute);

    /// <summary>
    /// Creates a layer whose content is rendered from a widget tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The widget tree is reconciled, measured, arranged, and rendered to a surface
    /// each frame. This is useful for transition effects where you want a non-interactive
    /// snapshot of a UI (e.g., a splash screen blending into the real application).
    /// </para>
    /// <para>
    /// Widget layers are non-interactive — they do not receive input events.
    /// </para>
    /// </remarks>
    /// <param name="widget">The widget tree to render as a layer.</param>
    /// <returns>A layer that renders the widget tree.</returns>
    public SurfaceLayer WidgetLayer(Hex1bWidget widget)
        => new WidgetSurfaceLayer(widget);
    
    /// <summary>
    /// Creates a tracked sixel from a pixel buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sixel is encoded from the pixel buffer and tracked for proper lifecycle management.
    /// The returned TrackedObject should be assigned to SurfaceCell.Sixel.
    /// </para>
    /// <para>
    /// Note: If no store is available (e.g., in tests), this returns null.
    /// </para>
    /// </remarks>
    /// <param name="buffer">The pixel buffer to encode as sixel.</param>
    /// <returns>A tracked sixel object, or null if sixel creation is not available.</returns>
    public TrackedObject<SixelData>? CreateSixel(SixelPixelBuffer buffer)
    {
        if (_store is null)
            return null;
            
        var payload = SixelEncoder.Encode(buffer);
        var (cellWidth, cellHeight) = CellMetrics.PixelToCellSpan(buffer.Width, buffer.Height);
        return _store.GetOrCreateSixel(payload, cellWidth, cellHeight);
    }
    
    /// <summary>
    /// Creates a tracked sixel from a pre-encoded sixel payload.
    /// </summary>
    /// <param name="payload">The sixel-encoded string.</param>
    /// <param name="widthInCells">Width in terminal cells.</param>
    /// <param name="heightInCells">Height in terminal cells.</param>
    /// <returns>A tracked sixel object, or null if sixel creation is not available.</returns>
    public TrackedObject<SixelData>? CreateSixel(string payload, int widthInCells, int heightInCells)
    {
        if (_store is null)
            return null;
            
        return _store.GetOrCreateSixel(payload, widthInCells, heightInCells);
    }
}
