using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node that renders a time series chart using braille characters for line drawing.
/// </summary>
public class TimeSeriesChartNode<T> : Hex1bNode
{
    public IReadOnlyList<T>? Data { get; set; }
    public Func<T, string>? LabelSelector { get; set; }
    public Func<T, double>? ValueSelector { get; set; }
    public IReadOnlyList<ChartSeriesDef<T>>? SeriesDefs { get; set; }
    public string? Title { get; set; }
    public bool ShowValues { get; set; }
    public bool ShowGridLines { get; set; } = true;
    public FillStyle FillStyle { get; set; } = FillStyle.None;
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public Func<double, string>? ValueFormatter { get; set; }

    public override Size Measure(Constraints constraints)
    {
        var width = constraints.MaxWidth == int.MaxValue ? 60 : constraints.MaxWidth;
        var height = constraints.MaxHeight == int.MaxValue ? 20 : constraints.MaxHeight;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect rect) => Bounds = rect;

    public override void Render(Hex1bRenderContext context)
    {
        if (Data is null || Data.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var width = Math.Min(Bounds.Width, 500);
        var height = Math.Min(Bounds.Height, 200);
        if (width <= 4 || height <= 3) return;

        // Resolve series data
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

        // Compute Y scaler from all series values
        var allValues = series.SelectMany(s => s.Values);
        ChartScaler yScaler;
        if (FillStyle != FillStyle.None && !Minimum.HasValue)
            yScaler = ChartScaler.FromValues(allValues, chartHeight, 0, Maximum);
        else
            yScaler = ChartScaler.FromValues(allValues, chartHeight, Minimum, Maximum);

        // Get colors
        var colors = ResolveSeriesColors(series);

        // Create surface
        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;
        var surface = new Surface(width, height, cellMetrics);

        // Grid lines
        if (ShowGridLines)
            AxisRenderer.DrawHorizontalGridLines(surface, yScaler, chartLeft, chartWidth, chartTop, chartHeight);

        // Render each series with braille
        for (int si = 0; si < series.Count; si++)
        {
            var s = series[si];
            var color = colors[si];
            var canvas = new BrailleCanvas(chartWidth, chartHeight);
            var pointCount = s.Values.Count;
            if (pointCount == 0) continue;

            // Map data points to dot coordinates
            var dotXs = new int[pointCount];
            var dotYs = new int[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                var xFrac = pointCount > 1 ? (double)i / (pointCount - 1) : 0.5;
                dotXs[i] = (int)Math.Round(xFrac * (canvas.DotWidth - 1));
                var yScaled = yScaler.Scale(s.Values[i]);
                dotYs[i] = canvas.DotHeight - 1 - (int)Math.Round(yScaled / chartHeight * (canvas.DotHeight - 1));
                dotYs[i] = Math.Clamp(dotYs[i], 0, canvas.DotHeight - 1);
            }

            // Draw lines between consecutive points
            for (int i = 0; i < pointCount - 1; i++)
                canvas.DrawLine(dotXs[i], dotYs[i], dotXs[i + 1], dotYs[i + 1]);

            // Area fill (single series only, or first series)
            if (FillStyle != FillStyle.None && (series.Count == 1 || si == 0))
            {
                if (FillStyle == FillStyle.Braille)
                {
                    // Fill below line with braille dots
                    for (int i = 0; i < pointCount; i++)
                    {
                        canvas.FillBelow(dotXs[i], dotYs[i]);
                        // Fill between points too
                        if (i < pointCount - 1)
                        {
                            var steps = Math.Abs(dotXs[i + 1] - dotXs[i]);
                            for (int step = 0; step <= steps; step++)
                            {
                                var t = steps > 0 ? (double)step / steps : 0;
                                var fx = dotXs[i] + t * (dotXs[i + 1] - dotXs[i]);
                                var fy = dotYs[i] + t * (dotYs[i + 1] - dotYs[i]);
                                canvas.FillBelow((int)Math.Round(fx), (int)Math.Round(fy));
                            }
                        }
                    }
                }
                else if (FillStyle == FillStyle.Solid)
                {
                    // Solid fill rendered with block chars (not braille)
                    DrawSolidFill(surface, s.Values, yScaler, color, chartLeft, chartTop, chartWidth, chartHeight);
                }
            }

            // Composite braille canvas onto surface
            for (int cy = 0; cy < canvas.CellHeight; cy++)
            for (int cx = 0; cx < canvas.CellWidth; cx++)
            {
                var ch = canvas.GetCell(cx, cy);
                if (ch is null) continue;

                var sx = chartLeft + cx;
                var sy = chartTop + cy;
                if (sx >= surface.Width || sy >= surface.Height) continue;

                // OR braille patterns if cell already has braille content
                var existing = surface[sx, sy];
                if (existing.Character is not null && existing.Character.Length == 1
                    && existing.Character[0] >= '\u2800' && existing.Character[0] <= '\u28FF')
                {
                    // OR the patterns, blend the colors
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
        AxisRenderer.DrawYAxis(surface, yScaler, yLabelWidth, chartTop, chartHeight, ValueFormatter);

        // X-axis labels
        var labels = ResolveLabels();
        if (labels.Count > 0)
            AxisRenderer.DrawXAxisLabels(surface, labels, chartLeft, chartWidth, chartTop + chartHeight,
                maxLabels: chartWidth / 6);

        // Title
        if (Title is not null && titleHeight > 0)
        {
            var titleX = Math.Max(0, (width - Title.Length) / 2);
            WriteText(surface, titleX, 0, Title, Hex1bColor.FromRgb(200, 200, 200));
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

    private record SeriesData(string Name, IReadOnlyList<double> Values);

    private List<SeriesData> ResolveSeries()
    {
        if (Data is null) return [];

        if (SeriesDefs is not null && SeriesDefs.Count > 0)
        {
            return SeriesDefs.Select(sd =>
                new SeriesData(sd.Name, Data.Select(sd.ValueSelector).ToList())
            ).ToList();
        }

        if (ValueSelector is not null)
        {
            return [new SeriesData("", Data.Select(ValueSelector).ToList())];
        }

        return [];
    }

    private List<string> ResolveLabels()
    {
        if (Data is null || LabelSelector is null) return [];
        return Data.Select(LabelSelector).ToList();
    }

    #endregion

    #region Drawing Helpers

    private static void DrawSolidFill(
        Surface surface, IReadOnlyList<double> values, ChartScaler yScaler, Hex1bColor color,
        int chartLeft, int chartTop, int chartWidth, int chartHeight)
    {
        // Dimmed fill color
        var fillColor = Hex1bColor.FromRgb(
            (byte)(color.R / 3), (byte)(color.G / 3), (byte)(color.B / 3));

        for (int i = 0; i < values.Count; i++)
        {
            var xFrac = values.Count > 1 ? (double)i / (values.Count - 1) : 0.5;
            var cellX = chartLeft + (int)Math.Round(xFrac * (chartWidth - 1));
            var yScaled = yScaler.Scale(values[i]);
            var topRow = chartHeight - 1 - (int)Math.Round(yScaled);
            topRow = Math.Clamp(topRow, 0, chartHeight - 1);

            // Fill from line down to bottom
            for (int row = topRow + 1; row < chartHeight; row++)
            {
                var sy = chartTop + row;
                if (sy >= surface.Height || cellX >= surface.Width) continue;
                var existing = surface[cellX, sy];
                if (existing.Character is null or " " or "·")
                    surface[cellX, sy] = new SurfaceCell("░", fillColor, null);
            }

            // Also fill between this point and next
            if (i < values.Count - 1)
            {
                var nextXFrac = (double)(i + 1) / (values.Count - 1);
                var nextCellX = chartLeft + (int)Math.Round(nextXFrac * (chartWidth - 1));
                var nextYScaled = yScaler.Scale(values[i + 1]);
                var nextTopRow = chartHeight - 1 - (int)Math.Round(nextYScaled);
                nextTopRow = Math.Clamp(nextTopRow, 0, chartHeight - 1);

                for (int cx = cellX; cx <= nextCellX && cx < chartLeft + chartWidth; cx++)
                {
                    var t = nextCellX > cellX ? (double)(cx - cellX) / (nextCellX - cellX) : 0;
                    var interpTop = (int)Math.Round(topRow + t * (nextTopRow - topRow));
                    for (int row = interpTop + 1; row < chartHeight; row++)
                    {
                        var sy = chartTop + row;
                        if (sy >= surface.Height || cx >= surface.Width) continue;
                        var existing = surface[cx, sy];
                        if (existing.Character is null or " " or "·")
                            surface[cx, sy] = new SurfaceCell("░", fillColor, null);
                    }
                }
            }
        }
    }

    private static Hex1bColor BlendColors(Hex1bColor? a, Hex1bColor b)
    {
        if (a is null) return b;
        return Hex1bColor.FromRgb(
            (byte)((a.Value.R + b.R) / 2),
            (byte)((a.Value.G + b.G) / 2),
            (byte)((a.Value.B + b.B) / 2));
    }

    private static Hex1bColor[] ResolveSeriesColors(List<SeriesData> series)
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
