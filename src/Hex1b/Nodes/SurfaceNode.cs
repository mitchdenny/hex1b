using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="SurfaceWidget"/>. Renders layered surfaces with compositing.
/// </summary>
/// <remarks>
/// <para>
/// SurfaceNode provides direct access to the Surface API, enabling arbitrary visualizations
/// with multiple composited layers. Each layer can be a static surface, dynamically drawn
/// content, or computed cells that can query layers below.
/// </para>
/// <para>
/// Layers are composited bottom-up in the order returned by the builder. The first layer
/// is at the bottom, and subsequent layers are drawn on top.
/// </para>
/// </remarks>
/// <seealso cref="SurfaceWidget"/>
/// <seealso cref="CompositeSurface"/>
public sealed class SurfaceNode : Hex1bNode
{
    private Size _measuredSize;

    /// <summary>
    /// Gets or sets the layer builder function that creates the layers to composite.
    /// </summary>
    /// <remarks>
    /// This function is called each render with a <see cref="SurfaceLayerContext"/>
    /// containing current mouse position, theme, and dimensions.
    /// </remarks>
    public Func<SurfaceLayerContext, IEnumerable<SurfaceLayer>>? LayerBuilder { get; set; }

    /// <summary>
    /// Measures the size required for this surface.
    /// </summary>
    /// <param name="constraints">The size constraints for layout.</param>
    /// <returns>The measured size based on size hints.</returns>
    public override Size Measure(Constraints constraints)
    {
        var width = WidthHint switch
        {
            { IsFixed: true } hint => Math.Min(hint.FixedValue, constraints.MaxWidth),
            { IsFill: true } => constraints.MaxWidth,
            _ => constraints.MaxWidth // Content defaults to fill for surfaces
        };

        var height = HeightHint switch
        {
            { IsFixed: true } hint => Math.Min(hint.FixedValue, constraints.MaxHeight),
            { IsFill: true } => constraints.MaxHeight,
            _ => constraints.MaxHeight // Content defaults to fill for surfaces
        };

        _measuredSize = constraints.Constrain(new Size(width, height));
        return _measuredSize;
    }

    /// <summary>
    /// Renders the layered surfaces to the context.
    /// </summary>
    /// <param name="context">The render context.</param>
    public override void Render(Hex1bRenderContext context)
    {
        if (LayerBuilder is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        // Clamp to reasonable size - typical terminal is 80x24 to 300x80
        // Anything larger is likely an error (unbounded constraints)
        var width = Math.Min(Bounds.Width, 500);
        var height = Math.Min(Bounds.Height, 200);
        
        if (width <= 0 || height <= 0)
            return;

        // Create context for the layer builder
        // TODO: Get actual mouse position from input system when available
        var layerContext = new SurfaceLayerContext(
            width: width,
            height: height,
            mouseX: -1,
            mouseY: -1,
            theme: context.Theme);

        // Build the layers
        var layers = LayerBuilder(layerContext).ToList();
        if (layers.Count == 0)
            return;

        // Create a composite surface and add all layers
        var composite = new CompositeSurface(width, height);

        foreach (var layer in layers)
        {
            switch (layer)
            {
                case SourceSurfaceLayer source:
                    composite.AddLayer(source.Source, source.OffsetX, source.OffsetY);
                    break;

                case DrawSurfaceLayer draw:
                    // Create a surface for the draw callback
                    var drawSurface = new Surface(width, height);
                    draw.Draw(drawSurface);
                    composite.AddLayer(drawSurface, draw.OffsetX, draw.OffsetY);
                    break;

                case ComputedSurfaceLayer computed:
                    // Computed layers span the entire surface
                    composite.AddComputedLayer(width, height, computed.Compute);
                    break;
            }
        }

        // Flatten and render to the context
        var flattened = composite.Flatten();

        // Optimized path: if rendering to a SurfaceRenderContext, composite directly
        if (context is SurfaceRenderContext surfaceContext)
        {
            surfaceContext.Surface.Composite(flattened, Bounds.X, Bounds.Y);
            return;
        }

        // Fallback: write each cell individually (slower, for legacy rendering)
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = flattened[x, y];
                
                // Skip unwritten/continuation cells
                if (cell.Character == SurfaceCells.UnwrittenMarker || cell.IsContinuation)
                    continue;

                // Build the ANSI-formatted string for this cell
                var text = BuildCellText(cell, context.Theme);
                context.WriteClipped(Bounds.X + x, Bounds.Y + y, text);
            }
        }
    }

    /// <summary>
    /// Builds the text representation for a cell, including ANSI formatting codes.
    /// </summary>
    private static string BuildCellText(SurfaceCell cell, Hex1bTheme theme)
    {
        var text = cell.Character;

        // Build SGR codes if needed
        var hasForeground = cell.Foreground is not null && !cell.Foreground.Value.IsDefault;
        var hasBackground = cell.Background is not null && !cell.Background.Value.IsDefault;
        var hasAttributes = cell.Attributes != CellAttributes.None;

        if (!hasForeground && !hasBackground && !hasAttributes)
            return text;

        var sb = new System.Text.StringBuilder();

        // Add foreground color
        if (hasForeground)
            sb.Append(cell.Foreground!.Value.ToForegroundAnsi());

        // Add background color
        if (hasBackground)
            sb.Append(cell.Background!.Value.ToBackgroundAnsi());

        // Add attributes
        if (cell.Attributes.HasFlag(CellAttributes.Bold))
            sb.Append("\x1b[1m");
        if (cell.Attributes.HasFlag(CellAttributes.Italic))
            sb.Append("\x1b[3m");
        if (cell.Attributes.HasFlag(CellAttributes.Underline))
            sb.Append("\x1b[4m");
        if (cell.Attributes.HasFlag(CellAttributes.Strikethrough))
            sb.Append("\x1b[9m");
        if (cell.Attributes.HasFlag(CellAttributes.Dim))
            sb.Append("\x1b[2m");
        if (cell.Attributes.HasFlag(CellAttributes.Reverse))
            sb.Append("\x1b[7m");

        if (sb.Length == 0)
            return text;

        sb.Append(text);
        sb.Append("\x1b[0m");
        return sb.ToString();
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        yield break;
    }
}
