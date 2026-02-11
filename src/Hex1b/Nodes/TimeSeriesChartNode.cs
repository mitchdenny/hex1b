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
    public ChartLayout Layout { get; set; } = ChartLayout.Simple;
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

        var isStacked = Layout == ChartLayout.Stacked && series.Count > 1;

        if (isStacked && FillStyle == FillStyle.Braille)
            throw new InvalidOperationException(
                "FillStyle.Braille is not supported with ChartLayout.Stacked. Use FillStyle.Solid instead.");

        // Auto-select solid fill for stacked area charts
        var effectiveFillStyle = isStacked && FillStyle == FillStyle.None ? FillStyle.Solid : FillStyle;

        // Compute cumulative values for stacked mode
        var pointCount = series[0].Values.Count;
        double[][] cumulative = null!;
        if (isStacked)
        {
            cumulative = new double[series.Count][];
            for (int si = 0; si < series.Count; si++)
            {
                cumulative[si] = new double[pointCount];
                for (int i = 0; i < pointCount; i++)
                {
                    cumulative[si][i] = series[si].Values[i]
                        + (si > 0 ? cumulative[si - 1][i] : 0);
                }
            }
        }

        // Compute Y scaler
        ChartScaler yScaler;
        if (isStacked)
        {
            var maxCumulative = cumulative[series.Count - 1].Max();
            yScaler = ChartScaler.FromValues([0, maxCumulative], chartHeight, 0, Maximum);
        }
        else
        {
            var allValues = series.SelectMany(s => s.Values);
            if (effectiveFillStyle != FillStyle.None && !Minimum.HasValue)
                yScaler = ChartScaler.FromValues(allValues, chartHeight, 0, Maximum);
            else
                yScaler = ChartScaler.FromValues(allValues, chartHeight, Minimum, Maximum);
        }

        var colors = ResolveSeriesColors(series);

        CellMetrics cellMetrics = CellMetrics.Default;
        if (context is SurfaceRenderContext surfaceCtx)
            cellMetrics = surfaceCtx.CellMetrics;
        var surface = new Surface(width, height, cellMetrics);

        if (ShowGridLines)
            AxisRenderer.DrawHorizontalGridLines(surface, yScaler, chartLeft, chartWidth, chartTop, chartHeight);

        if (isStacked)
            RenderStacked(surface, series, cumulative, yScaler, colors, chartLeft, chartTop, chartWidth, chartHeight, effectiveFillStyle);
        else
            RenderOverlaid(surface, series, yScaler, colors, chartLeft, chartTop, chartWidth, chartHeight, effectiveFillStyle);

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

    #region Rendering Modes

    private void RenderOverlaid(
        Surface surface, List<SeriesData> series, ChartScaler yScaler, Hex1bColor[] colors,
        int chartLeft, int chartTop, int chartWidth, int chartHeight, FillStyle fillStyle)
    {
        for (int si = 0; si < series.Count; si++)
        {
            var s = series[si];
            var color = colors[si];
            var canvas = new BrailleCanvas(chartWidth, chartHeight);
            var pointCount = s.Values.Count;
            if (pointCount == 0) continue;

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

            for (int i = 0; i < pointCount - 1; i++)
                canvas.DrawLine(dotXs[i], dotYs[i], dotXs[i + 1], dotYs[i + 1]);

            // Area fill (single series only, or first series)
            if (fillStyle != FillStyle.None && (series.Count == 1 || si == 0))
            {
                if (fillStyle == FillStyle.Braille)
                    FillBrailleBelow(canvas, dotXs, dotYs, pointCount);
                else if (fillStyle == FillStyle.Solid)
                    DrawSolidFill(surface, s.Values, yScaler, color, chartLeft, chartTop, chartWidth, chartHeight);
            }

            CompositeBraille(surface, canvas, color, chartLeft, chartTop);
        }
    }

    private void RenderStacked(
        Surface surface, List<SeriesData> series, double[][] cumulative, ChartScaler yScaler,
        Hex1bColor[] colors, int chartLeft, int chartTop, int chartWidth, int chartHeight, FillStyle fillStyle)
    {
        var pointCount = series[0].Values.Count;
        if (pointCount == 0) return;

        // Render from bottom (first series) to top (last series)
        for (int si = 0; si < series.Count; si++)
        {
            var color = colors[si];
            var topValues = cumulative[si];
            var bottomValues = si > 0 ? cumulative[si - 1] : null;

            // Stacked mode only supports solid fill (braille rejected earlier)
            var colorBelow = si > 0 ? colors[si - 1] : (Hex1bColor?)null;
            DrawStackedSolidFill(surface, topValues, bottomValues, yScaler, color, colorBelow,
                chartLeft, chartTop, chartWidth, chartHeight, pointCount);
        }
    }

    #endregion

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

    private static void CompositeBraille(Surface surface, BrailleCanvas canvas, Hex1bColor color,
        int chartLeft, int chartTop)
    {
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

    private static void FillBrailleBelow(BrailleCanvas canvas, int[] dotXs, int[] dotYs, int pointCount)
    {
        for (int i = 0; i < pointCount; i++)
        {
            canvas.FillBelow(dotXs[i], dotYs[i]);
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

    private static void FillBrailleBetween(BrailleCanvas canvas, int dotX, int topDotY, int bottomDotY)
    {
        if (dotX < 0 || dotX >= canvas.DotWidth) return;
        for (int y = Math.Max(0, topDotY); y <= Math.Min(canvas.DotHeight - 1, bottomDotY); y++)
            canvas.SetDot(dotX, y);
    }

    private static void DrawStackedSolidFill(
        Surface surface, double[] topValues, double[]? bottomValues, ChartScaler yScaler,
        Hex1bColor color, Hex1bColor? colorBelow, int chartLeft, int chartTop, int chartWidth, int chartHeight, int pointCount)
    {
        for (int cx = 0; cx < chartWidth && chartLeft + cx < surface.Width; cx++)
        {
            // Interpolate top and bottom values at this cell X
            var xFrac = chartWidth > 1 ? (double)cx / (chartWidth - 1) : 0.5;
            var dataIdx = xFrac * (pointCount - 1);
            var idx0 = (int)Math.Floor(dataIdx);
            var idx1 = Math.Min(idx0 + 1, pointCount - 1);
            var lerp = dataIdx - idx0;

            var topVal = topValues[idx0] + lerp * (topValues[idx1] - topValues[idx0]);
            var botVal = bottomValues is not null
                ? bottomValues[idx0] + lerp * (bottomValues[idx1] - bottomValues[idx0])
                : 0;

            var topScaled = yScaler.Scale(topVal);
            var botScaled = yScaler.Scale(botVal);

            // Convert to cell rows (0 = top of chart area)
            var topExact = chartHeight - 1 - topScaled;
            var botExact = chartHeight - 1 - botScaled;
            var topRow = (int)Math.Floor(topExact);
            var botRow = (int)Math.Floor(botExact);

            var sx = chartLeft + cx;

            // Fill full cells between top and bottom edges.
            // When there is no series below (bottommost), include botRow.
            var fullBotRow = bottomValues is null ? botRow + 1 : botRow;
            for (int row = Math.Max(0, topRow + 1); row < Math.Min(chartHeight, fullBotRow); row++)
            {
                var sy = chartTop + row;
                if (sy >= surface.Height) continue;
                surface[sx, sy] = new SurfaceCell("█", color, null);
            }

            // Fractional top edge: bottom portion of cell is this series
            if (topRow >= 0 && topRow < chartHeight)
            {
                var topFrac = topExact - topRow;
                var blockChar = FractionalBlocks.Vertical(1.0 - topFrac);
                var sy = chartTop + topRow;
                if (sy < surface.Height && blockChar != " ")
                    surface[sx, sy] = new SurfaceCell(blockChar, color, null);
            }

            // Fractional bottom edge: top portion = current series, bottom = series below.
            // Vertical blocks fill from bottom so FG = below color, BG = current color.
            if (botRow >= 0 && botRow < chartHeight && botRow != topRow && bottomValues is not null)
            {
                var botFrac = botExact - botRow;
                if (botFrac > 0.01)
                {
                    var blockChar = FractionalBlocks.Vertical(1.0 - botFrac);
                    var sy = chartTop + botRow;
                    if (sy < surface.Height)
                    {
                        if (blockChar == " ")
                            surface[sx, sy] = new SurfaceCell("█", color, null);
                        else
                            surface[sx, sy] = new SurfaceCell(blockChar, colorBelow ?? color, color);
                    }
                }
            }
        }
    }

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
