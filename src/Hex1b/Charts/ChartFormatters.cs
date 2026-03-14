namespace Hex1b.Charts;

/// <summary>
/// Provides default formatting utilities for chart axis labels and data values.
/// </summary>
internal static class ChartFormatters
{
    /// <summary>
    /// Formats a numeric value for display on a chart axis or data label.
    /// Produces compact, human-readable output adapted to the value's magnitude.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Zero → <c>"0"</c></description></item>
    /// <item><description>Small decimals → up to 2 significant fractional digits (e.g., <c>"0.42"</c>)</description></item>
    /// <item><description>Values under 10,000 → grouped digits, minimal decimals (e.g., <c>"1,234"</c>, <c>"42.5"</c>)</description></item>
    /// <item><description>10K–999K → suffix notation (e.g., <c>"12.3K"</c>)</description></item>
    /// <item><description>1M+ → suffix notation (e.g., <c>"1.2M"</c>, <c>"3.4B"</c>)</description></item>
    /// </list>
    /// </remarks>
    public static string FormatValue(double value)
    {
        if (value == 0)
            return "0";

        var abs = Math.Abs(value);

        // Very large values: use suffix notation
        if (abs >= 1_000_000_000)
            return FormatWithSuffix(value, 1_000_000_000, "B");
        if (abs >= 1_000_000)
            return FormatWithSuffix(value, 1_000_000, "M");
        if (abs >= 10_000)
            return FormatWithSuffix(value, 1_000, "K");

        // Values with no meaningful fractional part → integer with grouping
        if (abs >= 1 && Math.Abs(value - Math.Round(value)) < 0.005)
            return value.ToString("N0");

        // Values >= 1 with fractional part → show 1 decimal
        if (abs >= 1)
            return value.ToString("N1");

        // Small fractional values → up to 2 significant digits
        return value.ToString("G3");
    }

    private static string FormatWithSuffix(double value, double divisor, string suffix)
    {
        var scaled = value / divisor;
        var abs = Math.Abs(scaled);

        // 100+ → no decimals (e.g., "123K")
        if (abs >= 100)
            return $"{scaled:N0}{suffix}";

        // 10+ → one decimal (e.g., "12.3K")
        if (abs >= 10)
            return $"{scaled:F1}{suffix}";

        // < 10 → one decimal (e.g., "1.2M")
        return $"{scaled:F1}{suffix}";
    }
}
