namespace Hex1b.Charts;

/// <summary>
/// Specifies how multi-series data is displayed in a chart.
/// </summary>
public enum ChartMode
{
    /// <summary>
    /// Single series — one bar/column per category.
    /// </summary>
    Simple,

    /// <summary>
    /// Series segments are stacked end-to-end within each category.
    /// Column/bar height reflects the sum of actual values.
    /// </summary>
    Stacked,

    /// <summary>
    /// Series segments are stacked end-to-end, normalized so each category fills 100%.
    /// Segment sizes represent proportional share rather than absolute values.
    /// </summary>
    Stacked100,

    /// <summary>
    /// Series bars/columns are placed side-by-side within each category.
    /// </summary>
    Grouped
}
