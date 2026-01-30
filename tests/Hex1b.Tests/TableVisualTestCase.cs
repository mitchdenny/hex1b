using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Represents a test case configuration for table visual regression testing.
/// </summary>
/// <param name="Name">Unique name for the test case, used for baseline file naming.</param>
/// <param name="RowCount">Number of data rows (0 = no data).</param>
/// <param name="Mode">Render mode: Compact or Full.</param>
/// <param name="Width">Terminal width in columns.</param>
/// <param name="Height">Terminal height in rows.</param>
/// <param name="HasSelection">Whether to show selection column.</param>
/// <param name="DataSourceType">Type of data source: Sync or Async.</param>
/// <param name="Category">Test category for baseline organization.</param>
/// <param name="FocusedRow">Optional: which row has focus (-1 = none).</param>
/// <param name="SelectedRows">Optional: indices of selected rows.</param>
/// <param name="ScrollPosition">Optional: starting scroll position.</param>
/// <param name="TableHasFocus">Whether the table itself has focus (for border indicator).</param>
public record TableVisualTestCase(
    string Name,
    int RowCount,
    TableRenderMode Mode,
    int Width,
    int Height,
    bool HasSelection = false,
    TableDataSourceType DataSourceType = TableDataSourceType.Sync,
    TableTestCategory Category = TableTestCategory.Structure,
    int FocusedRow = -1,
    int[]? SelectedRows = null,
    int ScrollPosition = 0,
    bool TableHasFocus = false)
{
    /// <summary>
    /// Gets the baseline file name (without extension).
    /// </summary>
    public string BaselineName => $"{Category}/{Name}";
    
    public override string ToString() => Name;
}

/// <summary>
/// Type of data source.
/// </summary>
public enum TableDataSourceType
{
    Sync,
    Async
}

/// <summary>
/// Category for organizing baselines.
/// </summary>
public enum TableTestCategory
{
    Structure,
    Selection,
    Async,
    Focus,
    TableFocus
}
