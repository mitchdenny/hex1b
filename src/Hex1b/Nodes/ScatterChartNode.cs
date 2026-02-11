using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node that renders a scatter plot using braille characters for sub-cell dot plotting.
/// </summary>
public class ScatterChartNode<T> : Hex1bNode
{
    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, double>? XSelector { get; set; }
    public Func<T, double>? YSelector { get; set; }
    public Func<T, string>? GroupBySelector { get; set; }
    public string? Title { get; set; }
    public bool ShowGridLines { get; set; } = true;
    public double? XMin { get; set; }
    public double? XMax { get; set; }
    public double? YMin { get; set; }
    public double? YMax { get; set; }
    public Func<double, string>? XFormatter { get; set; }
    public Func<double, string>? YFormatter { get; set; }

    public override Size Measure(Constraints constraints)
    {
        var width = constraints.MaxWidth == int.MaxValue ? 60 : constraints.MaxWidth;
        var height = constraints.MaxHeight == int.MaxValue ? 20 : constraints.MaxHeight;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect rect) => Bounds = rect;

    public override void Render(Hex1bRenderContext context)
    {
        if (Data is null || Data.Count == 0 || XSelector is null || YSelector is null
            || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var width = Math.Min(Bounds.Width, 500);
        var height = Math.Min(Bounds.Height, 200);
        if (width <= 4 || height <= 3) return;

        // Resolve grouped series
        var series = ResolveSeries();
        if (series.Count == 0) return;

        // Layout regions
        var titleHeight = Title is not null ? 1 : 0;
        var xLabelHeight = 1;
        var yLabelWidth = 6;
        var chartWidth = width - yLabelWidth;
        var chartHeight = height - titleHeight - xLabelHeight;
        if (chartWidth <= 2 || chartHeight <= 1) return;
        var chartLeft = yLabelWidth;
        var chartTop = titleHeight;

        // Compute scalers
        var allX = series.SelectMany(s => s.Points.Select(p => p.X));
        var allY = series.SelectMany(s => s.Points.Select(p => p.Y));
        var xScaler = ChartScaler.FromValues(allX, chartWidth, XMin, XMax);
        var yScaler = ChartScaler.FromValues(allY, chartHeight, YMin, YMax);

        var colors = ResolveSeriesColors(series);

        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;
        var surface = new Surface(width, height, cellMetrics);

        // Grid lines
        if (ShowGridLines)
            AxisRenderer.DrawHorizontalGridLines(surface, yScaler, chartLeft, chartWidth, chartTop, chartHeight);

        // Render each series
        for (int si = 0; si < series.Count; si++)
        {
            var s = series[si];
            var color = colors[si];
            var canvas = new BrailleCanvas(chartWidth, chartHeight);

            foreach (var pt in s.Points)
            {
                var xScaled = xScaler.Scale(pt.X);
                var yScaled = yScaler.Scale(pt.Y);

                var dotX = (int)Math.Round(xScaled / chartWidth * (canvas.DotWidth - 1));
                var dotY = canvas.DotHeight - 1 - (int)Math.Round(yScaled / chartHeight * (canvas.DotHeight - 1));
                dotX = Math.Clamp(dotX, 0, canvas.DotWidth - 1);
                dotY = Math.Clamp(dotY, 0, canvas.DotHeight - 1);
                canvas.SetDot(dotX, dotY);
            }

            // Composite braille canvas
            for (int cy = 0; cy < canvas.CellHeight; cy++)
            for (int cx = 0; cx < canvas.CellWidth; cx++)
            {
                var ch = canvas.GetCell(cx, cy);
                if (ch is null) continue;

                var sx = chartLeft + cx;
                var sy = chartTop + cy;
                if (sx >= surface.Width || sy >= surface.Height) continue;

                var existing = surface[sx, sy];
                if (existing.Character is not null && existing.Character.Length == 1
                    && existing.Character[0] >= '\u2800' && existing.Character[0] <= '\u28FF')
                {
                    var merged = (char)(existing.Character[0] | ch.Value);
                    var blendedFg = BlendColors(existing.Foreground, color);
                    surface[sx, sy] = new SurfaceCell(merged.ToString(), blendedFg, existing.Background);
                }
                else
                {
                    surface[sx, sy] = new SurfaceCell(ch.Value.ToString(), color, null);
                }
            }
        }

        // Y-axis labels
        AxisRenderer.DrawYAxis(surface, yScaler, yLabelWidth, chartTop, chartHeight, YFormatter);

        // X-axis labels (using tick values from xScaler)
        var xTicks = AxisRenderer.ComputeNiceTicks(xScaler.Minimum, xScaler.Maximum, chartWidth / 8);
        var xLabels = xTicks.Select(v => XFormatter is not null ? XFormatter(v) : v.ToString("G3")).ToList();
        AxisRenderer.DrawXAxisLabels(surface, xLabels, chartLeft, chartWidth, chartTop + chartHeight,
            maxLabels: chartWidth / 6);

        // Title
        if (Title is not null && titleHeight > 0)
        {
            var titleX = Math.Max(0, (width - Title.Length) / 2);
            WriteText(surface, titleX, 0, Title, Hex1bColor.FromRgb(200, 200, 200));
        }

        // Legend for grouped data
        if (series.Count > 1)
        {
            var legendY = chartTop + chartHeight; // on x-label row, right-aligned
            var legendX = width;
            for (int i = series.Count - 1; i >= 0; i--)
            {
                var label = $" ● {series[i].Name}";
                legendX -= label.Length;
                if (legendX < chartLeft) break;
                WriteText(surface, legendX, legendY, label, colors[i]);
            }
        }

        // Composite to render context
        if (context is SurfaceRenderContext surfCtx)
        {
            var destX = Bounds.X - surfCtx.OffsetX;
            var destY = Bounds.Y - surfCtx.OffsetY;
            surfCtx.Surface.Composite(surface, destX, destY);
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren() { yield break; }

    #region Data Resolution

    private record ScatterPoint(double X, double Y);
    private record ScatterSeries(string Name, IReadOnlyList<ScatterPoint> Points);

    private List<ScatterSeries> ResolveSeries()
    {
        if (Data is null || XSelector is null || YSelector is null) return [];

        if (GroupBySelector is not null)
        {
            return Data
                .GroupBy(GroupBySelector)
                .Select(g => new ScatterSeries(
                    g.Key,
                    g.Select(item => new ScatterPoint(XSelector(item), YSelector(item))).ToList()))
                .ToList();
        }

        return [new ScatterSeries("",
            Data.Select(item => new ScatterPoint(XSelector(item), YSelector(item))).ToList())];
    }

    #endregion

    #region Drawing Helpers

    private static Hex1bColor BlendColors(Hex1bColor? a, Hex1bColor b)
    {
        if (a is null) return b;
        return Hex1bColor.FromRgb(
            (byte)((a.Value.R + b.R) / 2),
            (byte)((a.Value.G + b.G) / 2),
            (byte)((a.Value.B + b.B) / 2));
    }

    private static Hex1bColor[] ResolveSeriesColors(List<ScatterSeries> series)
    {
        Hex1bColor[] palette =
        [
            Hex1bColor.FromRgb(66, 133, 244),
            Hex1bColor.FromRgb(234, 67, 53),
            Hex1bColor.FromRgb(52, 168, 83),
            Hex1bColor.FromRgb(251, 188, 4),
            Hex1bColor.FromRgb(171, 71, 188),
            Hex1bColor.FromRgb(0, 172, 193),
        ];
        return series.Select((_, i) => palette[i % palette.Length]).ToArray();
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

    #endregion
}
