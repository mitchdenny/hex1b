using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="BreakdownChartWidget{T}"/>.
/// Renders a proportional segmented bar with an optional legend.
/// </summary>
public sealed class BreakdownChartNode<T> : Hex1bNode
{
    private Size _measuredSize;

    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, string>? LabelSelector { get; set; }
    public Func<T, double>? ValueSelector { get; set; }
    public bool ShowValues { get; set; }
    public bool ShowPercentages { get; set; }
    public string? Title { get; set; }

    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
    {
        // Bar (1 row) + legend rows + title
        var legendRows = Data?.Count ?? 0;
        var titleHeight = Title is not null ? 1 : 0;
        var idealHeight = titleHeight + 1 + legendRows; // title + bar + legend

        var width = constraints.MaxWidth == int.MaxValue ? 40 : constraints.MaxWidth;
        var height = Math.Min(idealHeight, constraints.MaxHeight);
        _measuredSize = constraints.Constrain(new Size(width, Math.Max(2, height)));
        return _measuredSize;
    }

    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (Data is null || Data.Count == 0 || LabelSelector is null || ValueSelector is null)
            return;
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var width = Math.Min(Bounds.Width, 500);
        var height = Math.Min(Bounds.Height, 200);
        if (width <= 2) return;

        // Resolve segments
        var segments = new List<BreakdownSegment>();
        double total = 0;
        foreach (var item in Data)
        {
            var label = LabelSelector(item);
            var value = ValueSelector(item);
            if (value > 0)
            {
                segments.Add(new(label, value));
                total += value;
            }
        }
        if (segments.Count == 0 || total <= 0) return;

        var colors = ResolveColors(segments.Count);

        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;

        var surface = new Surface(width, height, cellMetrics);

        var titleHeight = Title is not null ? 1 : 0;
        var barY = titleHeight;

        // Title
        if (Title is not null && titleHeight > 0)
        {
            var labelColor = Hex1bColor.FromRgb(200, 200, 200);
            var titleX = Math.Max(0, (width - Title.Length) / 2);
            WriteText(surface, titleX, 0, Title, labelColor);
        }

        // Draw the proportional bar
        DrawBar(surface, segments, colors, total, barY, width);

        // Draw the legend
        DrawLegend(surface, segments, colors, total, barY + 1, width, height);

        // Composite
        if (context is SurfaceRenderContext surfCtx)
        {
            var destX = Bounds.X - surfCtx.OffsetX;
            var destY = Bounds.Y - surfCtx.OffsetY;
            surfCtx.Surface.Composite(surface, destX, destY);
        }
    }

    /// <inheritdoc />
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        yield break;
    }

    private static void DrawBar(
        Surface surface,
        List<BreakdownSegment> segments,
        Hex1bColor[] colors,
        double total,
        int y,
        int width)
    {
        if (y >= surface.Height) return;

        double xAccumulator = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            var proportion = segments[i].Value / total;
            var segmentWidth = proportion * width;
            var startX = (int)xAccumulator;
            xAccumulator += segmentWidth;
            var endX = (int)xAccumulator;

            var color = colors[i % colors.Length];

            for (int x = startX; x < endX && x < width; x++)
            {
                if (y >= 0 && y < surface.Height)
                    surface[x, y] = new SurfaceCell(" ", null, color);
            }

            // Fractional right edge
            if (endX < width && y >= 0 && y < surface.Height)
            {
                var frac = xAccumulator - endX;
                if (frac > 0.05)
                {
                    var blockChar = FractionalBlocks.Horizontal(frac);
                    surface[endX, y] = new SurfaceCell(blockChar, color, null);
                }
            }
        }
    }

    private void DrawLegend(
        Surface surface,
        List<BreakdownSegment> segments,
        Hex1bColor[] colors,
        double total,
        int startY,
        int width,
        int height)
    {
        var labelColor = Hex1bColor.FromRgb(200, 200, 200);
        var dimColor = Hex1bColor.FromRgb(140, 140, 140);

        for (int i = 0; i < segments.Count; i++)
        {
            var y = startY + i;
            if (y >= surface.Height) break;

            var seg = segments[i];
            var color = colors[i % colors.Length];

            // Color swatch
            if (0 < surface.Width) surface[0, y] = new SurfaceCell("█", color, null);
            if (1 < surface.Width) surface[1, y] = new SurfaceCell(" ", null, null);

            // Label
            var x = 2;
            WriteText(surface, x, y, seg.Label, labelColor);
            x += seg.Label.Length;

            // Value / percentage
            if (ShowValues || ShowPercentages)
            {
                var suffix = "";
                if (ShowValues && ShowPercentages)
                {
                    var pct = seg.Value / total * 100;
                    suffix = $" ({seg.Value:G4}, {pct:F1}%)";
                }
                else if (ShowValues)
                {
                    suffix = $" ({seg.Value:G4})";
                }
                else if (ShowPercentages)
                {
                    var pct = seg.Value / total * 100;
                    suffix = $" ({pct:F1}%)";
                }

                WriteText(surface, x, y, suffix, dimColor);
            }
        }
    }

    private static void WriteText(Surface surface, int x, int y, string text, Hex1bColor color)
    {
        if (y < 0 || y >= surface.Height) return;
        for (int i = 0; i < text.Length && x + i < surface.Width; i++)
        {
            if (x + i < 0) continue;
            surface[x + i, y] = new SurfaceCell(text[i].ToString(), color, null);
        }
    }

    private static Hex1bColor[] ResolveColors(int count)
    {
        Hex1bColor[] palette =
        [
            Hex1bColor.FromRgb(66, 133, 244),
            Hex1bColor.FromRgb(234, 67, 53),
            Hex1bColor.FromRgb(52, 168, 83),
            Hex1bColor.FromRgb(251, 188, 4),
            Hex1bColor.FromRgb(171, 71, 188),
            Hex1bColor.FromRgb(0, 172, 193),
            Hex1bColor.FromRgb(255, 112, 67),
            Hex1bColor.FromRgb(158, 157, 36),
        ];

        var colors = new Hex1bColor[count];
        for (int i = 0; i < count; i++)
            colors[i] = palette[i % palette.Length];
        return colors;
    }

    private record BreakdownSegment(string Label, double Value);
}
