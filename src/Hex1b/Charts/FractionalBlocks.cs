namespace Hex1b.Charts;

/// <summary>
/// Provides Unicode block characters for sub-cell precision rendering in charts.
/// </summary>
/// <remarks>
/// <para>
/// Terminal cells are typically ~2:1 aspect ratio (taller than wide). Both horizontal
/// and vertical block character sets provide 8 levels of sub-cell precision.
/// </para>
/// </remarks>
internal static class FractionalBlocks
{
    /// <summary>
    /// Horizontal block characters for bar charts (left-to-right fill).
    /// Ordered by fill fraction: ▏ (1/8) through █ (8/8).
    /// </summary>
    private static readonly string[] HorizontalBlocks =
    [
        " ", // 0/8 — empty
        "▏", // 1/8
        "▎", // 2/8
        "▍", // 3/8
        "▌", // 4/8
        "▋", // 5/8
        "▊", // 6/8
        "▉", // 7/8
        "█", // 8/8 — full
    ];

    /// <summary>
    /// Vertical block characters for column charts (bottom-to-top fill).
    /// Ordered by fill fraction: ▁ (1/8) through █ (8/8).
    /// </summary>
    private static readonly string[] VerticalBlocks =
    [
        " ", // 0/8 — empty
        "▁", // 1/8
        "▂", // 2/8
        "▃", // 3/8
        "▄", // 4/8
        "▅", // 5/8
        "▆", // 6/8
        "▇", // 7/8
        "█", // 8/8 — full
    ];

    /// <summary>
    /// Gets the horizontal block character for a fractional fill amount.
    /// </summary>
    /// <param name="fraction">The fill fraction (0.0 to 1.0) within a single cell.</param>
    /// <returns>The appropriate block character.</returns>
    public static string Horizontal(double fraction)
    {
        var index = (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * 8);
        return HorizontalBlocks[index];
    }

    /// <summary>
    /// Gets the vertical block character for a fractional fill amount.
    /// </summary>
    /// <param name="fraction">The fill fraction (0.0 to 1.0) within a single cell.</param>
    /// <returns>The appropriate block character.</returns>
    public static string Vertical(double fraction)
    {
        var index = (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * 8);
        return VerticalBlocks[index];
    }

    /// <summary>
    /// Decomposes a fractional cell count into whole cells and a fractional remainder.
    /// </summary>
    /// <param name="cells">The total fractional cell count (e.g., 3.625).</param>
    /// <returns>The number of whole full cells and the fractional remainder (0.0 to 1.0).</returns>
    public static (int wholeCells, double remainder) Decompose(double cells)
    {
        var whole = (int)cells;
        var remainder = cells - whole;
        return (whole, remainder);
    }
}
