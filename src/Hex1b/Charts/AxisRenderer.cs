using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1b.Charts;

/// <summary>
/// Shared utilities for rendering chart axes, grid lines, and tick marks.
/// </summary>
internal static class AxisRenderer
{
    private static readonly Hex1bColor AxisColor = Hex1bColor.FromRgb(120, 120, 120);
    private static readonly Hex1bColor GridColor = Hex1bColor.FromRgb(60, 60, 60);
    private static readonly Hex1bColor LabelColor = Hex1bColor.FromRgb(180, 180, 180);

    /// <summary>
    /// Computes "nice" tick values for an axis given a data range.
    /// </summary>
    public static double[] ComputeNiceTicks(double min, double max, int maxTicks)
    {
        if (max <= min || maxTicks <= 0)
            return [min];

        var range = NiceNumber(max - min, false);
        var tickSpacing = NiceNumber(range / (maxTicks - 1), true);
        var niceMin = Math.Floor(min / tickSpacing) * tickSpacing;
        var niceMax = Math.Ceiling(max / tickSpacing) * tickSpacing;

        var ticks = new List<double>();
        for (var t = niceMin; t <= niceMax + tickSpacing * 0.5; t += tickSpacing)
        {
            if (t >= min - tickSpacing * 0.01 && t <= max + tickSpacing * 0.01)
                ticks.Add(t);
        }

        return ticks.ToArray();
    }

    /// <summary>
    /// Draws Y-axis labels on the left side of the chart area.
    /// </summary>
    /// <param name="surface">The surface to draw on.</param>
    /// <param name="scaler">The Y-axis scaler.</param>
    /// <param name="labelWidth">Width reserved for labels.</param>
    /// <param name="topOffset">Top edge of the chart area.</param>
    /// <param name="chartHeight">Height of the chart area.</param>
    /// <param name="formatter">Value formatter.</param>
    public static void DrawYAxis(
        Surface surface, ChartScaler scaler, int labelWidth, int topOffset, int chartHeight,
        Func<double, string>? formatter = null)
    {
        var fmt = formatter ?? (v => v.ToString("G4"));
        var ticks = ComputeNiceTicks(scaler.Minimum, scaler.Maximum, Math.Min(6, chartHeight / 2));

        foreach (var tick in ticks)
        {
            var scaled = scaler.Scale(tick);
            var y = topOffset + chartHeight - 1 - (int)Math.Round(scaled);
            if (y < topOffset || y >= topOffset + chartHeight) continue;

            var text = fmt(tick);
            if (text.Length > labelWidth - 1)
                text = text[..(labelWidth - 1)];

            // Right-align label
            var x = Math.Max(0, labelWidth - 1 - text.Length);
            WriteText(surface, x, y, text, LabelColor);
        }
    }

    /// <summary>
    /// Draws X-axis labels along the bottom of the chart area.
    /// </summary>
    /// <param name="surface">The surface to draw on.</param>
    /// <param name="labels">The category labels to display.</param>
    /// <param name="chartLeft">Left edge of the chart area.</param>
    /// <param name="chartWidth">Width of the chart area.</param>
    /// <param name="y">Y position for the labels.</param>
    /// <param name="maxLabels">Maximum number of labels to show (auto-skip for density).</param>
    public static void DrawXAxisLabels(
        Surface surface, IReadOnlyList<string> labels, int chartLeft, int chartWidth, int y,
        int maxLabels = 0)
    {
        if (labels.Count == 0 || y >= surface.Height || y < 0) return;

        var count = labels.Count;
        var step = maxLabels > 0 && count > maxLabels
            ? (int)Math.Ceiling((double)count / maxLabels)
            : 1;

        for (int i = 0; i < count; i += step)
        {
            var x = chartLeft + (int)((double)i / Math.Max(1, count - 1) * (chartWidth - 1));
            var label = labels[i];
            // Center the label on the data point position
            var labelX = x - label.Length / 2;
            labelX = Math.Max(chartLeft, Math.Min(labelX, chartLeft + chartWidth - label.Length));
            WriteText(surface, labelX, y, label, LabelColor);
        }
    }

    /// <summary>
    /// Draws horizontal grid lines across the chart area at Y-axis tick positions.
    /// </summary>
    public static void DrawHorizontalGridLines(
        Surface surface, ChartScaler yScaler, int chartLeft, int chartWidth,
        int topOffset, int chartHeight)
    {
        var ticks = ComputeNiceTicks(yScaler.Minimum, yScaler.Maximum, Math.Min(6, chartHeight / 2));

        foreach (var tick in ticks)
        {
            var scaled = yScaler.Scale(tick);
            var y = topOffset + chartHeight - 1 - (int)Math.Round(scaled);
            if (y < topOffset || y >= topOffset + chartHeight) continue;

            for (int x = chartLeft; x < chartLeft + chartWidth && x < surface.Width; x++)
            {
                // Only draw on empty cells (don't overwrite data)
                var existing = surface[x, y];
                if (existing.Character is null or " ")
                    surface[x, y] = new SurfaceCell("·", GridColor, null);
            }
        }
    }

    /// <summary>
    /// Computes a "nice" number — a rounded value suitable for axis ticks.
    /// </summary>
    private static double NiceNumber(double range, bool round)
    {
        var exponent = Math.Floor(Math.Log10(range));
        var fraction = range / Math.Pow(10, exponent);

        double niceFraction;
        if (round)
        {
            niceFraction = fraction switch
            {
                < 1.5 => 1,
                < 3 => 2,
                < 7 => 5,
                _ => 10
            };
        }
        else
        {
            niceFraction = fraction switch
            {
                <= 1 => 1,
                <= 2 => 2,
                <= 5 => 5,
                _ => 10
            };
        }

        return niceFraction * Math.Pow(10, exponent);
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
}
