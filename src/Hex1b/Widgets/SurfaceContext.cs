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
/// </list>
/// </para>
/// <para>
/// The context is created fresh for each render, with current mouse
/// position and theme state.
/// </para>
/// </remarks>
public class SurfaceLayerContext
{
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
    /// Creates a new SurfaceLayerContext.
    /// </summary>
    internal SurfaceLayerContext(int width, int height, int mouseX, int mouseY, Hex1bTheme theme)
    {
        Width = width;
        Height = height;
        MouseX = mouseX;
        MouseY = mouseY;
        Theme = theme;
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
}
