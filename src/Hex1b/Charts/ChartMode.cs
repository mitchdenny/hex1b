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
    /// </summary>
    Stacked,

    /// <summary>
    /// Series bars/columns are placed side-by-side within each category.
    /// </summary>
    Grouped
}
