using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="DonutChartWidget{T}"/>.
/// Renders a donut (or pie) chart using half-block characters for 2× vertical resolution.
/// </summary>
public sealed class DonutChartNode<T> : Hex1bNode
{
    private const char UpperHalf = '▀';
    private const char LowerHalf = '▄';

    private Size _measuredSize;

    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, string>? LabelSelector { get; set; }
    public Func<T, double>? ValueSelector { get; set; }
    public string? Title { get; set; }
    public double HoleSizeRatio { get; set; } = 0.5;

    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
    {
        var titleHeight = Title is not null ? 1 : 0;

        var availWidth = constraints.MaxWidth == int.MaxValue ? 40 : constraints.MaxWidth;
        // Donut diameter in pixels = availWidth, cell rows = ceil(diameter / 2)
        var donutCellRows = (availWidth + 1) / 2;
        var idealHeight = titleHeight + donutCellRows;

        var width = availWidth;
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
        var segments = new List<DonutSegment>();
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

        // Title
        if (Title is not null && titleHeight > 0)
        {
            var labelColor = Hex1bColor.FromRgb(200, 200, 200);
            var titleX = Math.Max(0, (width - Title.Length) / 2);
            WriteText(surface, titleX, 0, Title, labelColor);
        }

        // Calculate donut dimensions
        var donutCellRows = Math.Max(1, height - titleHeight);
        var donutPixelHeight = donutCellRows * 2; // half-block gives 2 pixels per cell row
        var donutPixelWidth = width;

        // The donut should be circular — use the smaller dimension as diameter
        var diameter = Math.Min(donutPixelWidth, donutPixelHeight);
        if (diameter < 4) diameter = Math.Max(donutPixelWidth, donutPixelHeight);

        var centerX = donutPixelWidth / 2.0;
        var centerY = donutPixelHeight / 2.0;
        var outerRadius = diameter / 2.0 - 0.5;
        var innerRadius = outerRadius * Math.Clamp(HoleSizeRatio, 0.0, 0.95);

        // Build pixel grid
        var pixels = new int[donutPixelWidth, donutPixelHeight]; // -1 = empty, >= 0 = segment index
        for (int py = 0; py < donutPixelHeight; py++)
        for (int px = 0; px < donutPixelWidth; px++)
            pixels[px, py] = -1;

        // Precompute segment angles
        var segmentAngles = new double[segments.Count + 1];
        segmentAngles[0] = -Math.PI / 2; // Start at top (12 o'clock)
        for (int i = 0; i < segments.Count; i++)
        {
            var proportion = segments[i].Value / total;
            segmentAngles[i + 1] = segmentAngles[i] + proportion * 2 * Math.PI;
        }

        // Fill pixels
        for (int py = 0; py < donutPixelHeight; py++)
        {
            for (int px = 0; px < donutPixelWidth; px++)
            {
                var dx = px + 0.5 - centerX;
                var dy = py + 0.5 - centerY;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < innerRadius || dist > outerRadius)
                    continue;

                // Single segment covers the full circle
                if (segments.Count == 1)
                {
                    pixels[px, py] = 0;
                    continue;
                }

                var angle = Math.Atan2(dy, dx);

                // Find which segment this angle belongs to
                for (int i = 0; i < segments.Count; i++)
                {
                    if (AngleInRange(angle, segmentAngles[i], segmentAngles[i + 1]))
                    {
                        pixels[px, py] = i;
                        break;
                    }
                }
            }
        }

        // Pack pixel pairs into half-block cells
        var donutStartY = titleHeight;
        for (int cellRow = 0; cellRow < donutCellRows && donutStartY + cellRow < height; cellRow++)
        {
            var topPixelY = cellRow * 2;
            var botPixelY = cellRow * 2 + 1;

            for (int px = 0; px < donutPixelWidth && px < width; px++)
            {
                var topSeg = topPixelY < donutPixelHeight ? pixels[px, topPixelY] : -1;
                var botSeg = botPixelY < donutPixelHeight ? pixels[px, botPixelY] : -1;

                if (topSeg == -1 && botSeg == -1)
                    continue; // Both empty, leave cell blank

                var cellY = donutStartY + cellRow;

                if (topSeg >= 0 && botSeg >= 0)
                {
                    if (topSeg == botSeg)
                    {
                        // Same segment — full block with bg color
                        surface[px, cellY] = new SurfaceCell(" ", null, colors[topSeg]);
                    }
                    else
                    {
                        // Different segments — upper half shows top as fg, bottom as bg
                        surface[px, cellY] = new SurfaceCell(
                            UpperHalf.ToString(), colors[topSeg], colors[botSeg]);
                    }
                }
                else if (topSeg >= 0)
                {
                    // Only top pixel filled
                    surface[px, cellY] = new SurfaceCell(UpperHalf.ToString(), colors[topSeg], null);
                }
                else
                {
                    // Only bottom pixel filled
                    surface[px, cellY] = new SurfaceCell(LowerHalf.ToString(), colors[botSeg], null);
                }
            }
        }

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

    private static void WriteText(Surface surface, int x, int y, string text, Hex1bColor color)
    {
        if (y < 0 || y >= surface.Height) return;
        for (int i = 0; i < text.Length && x + i < surface.Width; i++)
        {
            if (x + i < 0) continue;
            surface[x + i, y] = new SurfaceCell(text[i].ToString(), color, null);
        }
    }

    /// <summary>
    /// Checks whether an angle falls within a range defined by start and end angles
    /// (which increase counter-clockwise from -π/2).
    /// </summary>
    private static bool AngleInRange(double angle, double rangeStart, double rangeEnd)
    {
        // Normalize all to [0, 2π)
        var a = ((angle % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
        var s = ((rangeStart % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
        var e = ((rangeEnd % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);

        if (s <= e)
            return a >= s && a < e;
        else
            return a >= s || a < e; // Range wraps around 2π
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

    private record DonutSegment(string Label, double Value);
}
