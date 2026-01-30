using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Generates all table visual test cases for regression testing.
/// </summary>
public static class TableVisualTestCases
{
    /// <summary>
    /// All structure test cases (sync data, no selection).
    /// </summary>
    public static IEnumerable<object[]> StructureCases => GetStructureCases().Select(c => new object[] { c });
    
    /// <summary>
    /// All selection test cases.
    /// </summary>
    public static IEnumerable<object[]> SelectionCases => GetSelectionCases().Select(c => new object[] { c });
    
    /// <summary>
    /// All async loading test cases.
    /// </summary>
    public static IEnumerable<object[]> AsyncCases => GetAsyncCases().Select(c => new object[] { c });
    
    /// <summary>
    /// All row focus test cases.
    /// </summary>
    public static IEnumerable<object[]> FocusCases => GetFocusCases().Select(c => new object[] { c });
    
    /// <summary>
    /// All table focus indicator test cases.
    /// </summary>
    public static IEnumerable<object[]> TableFocusCases => GetTableFocusCases().Select(c => new object[] { c });

    private static IEnumerable<TableVisualTestCase> GetStructureCases()
    {
        // Data sizes
        int[] rowCounts = [0, 1, 5, 50, 1000];
        
        // Modes
        TableRenderMode[] modes = [TableRenderMode.Compact, TableRenderMode.Full];
        
        // Terminal sizes
        (int w, int h, string suffix)[] sizes = [(80, 24, "80x24"), (160, 48, "160x48")];
        
        foreach (var rowCount in rowCounts)
        {
            foreach (var mode in modes)
            {
                foreach (var (w, h, suffix) in sizes)
                {
                    var rowLabel = rowCount switch
                    {
                        0 => "NoData",
                        1 => "1Row",
                        _ => $"{rowCount}Rows"
                    };
                    
                    var modeLabel = mode == TableRenderMode.Compact ? "Compact" : "Full";
                    var name = $"{rowLabel}_{modeLabel}_{suffix}";
                    
                    yield return new TableVisualTestCase(
                        Name: name,
                        RowCount: rowCount,
                        Mode: mode,
                        Width: w,
                        Height: h,
                        Category: TableTestCategory.Structure);
                }
            }
        }
    }
    
    private static IEnumerable<TableVisualTestCase> GetSelectionCases()
    {
        // Selection column tests - varying row counts and modes
        yield return new TableVisualTestCase(
            Name: "1Row_Compact_80x24_Selection",
            RowCount: 1,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            HasSelection: true,
            Category: TableTestCategory.Selection);
        
        yield return new TableVisualTestCase(
            Name: "1Row_Full_80x24_Selection",
            RowCount: 1,
            Mode: TableRenderMode.Full,
            Width: 80,
            Height: 24,
            HasSelection: true,
            Category: TableTestCategory.Selection);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_80x24_Selection",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            HasSelection: true,
            Category: TableTestCategory.Selection);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Full_160x48_Selection",
            RowCount: 5,
            Mode: TableRenderMode.Full,
            Width: 160,
            Height: 48,
            HasSelection: true,
            Category: TableTestCategory.Selection);
        
        yield return new TableVisualTestCase(
            Name: "50Rows_Compact_80x24_Selection",
            RowCount: 50,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            HasSelection: true,
            Category: TableTestCategory.Selection);
        
        // Selection state tests
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_NoneSelected",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            HasSelection: true,
            SelectedRows: [],
            Category: TableTestCategory.Selection);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_SomeSelected",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            HasSelection: true,
            SelectedRows: [1, 3],
            Category: TableTestCategory.Selection);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_AllSelected",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            HasSelection: true,
            SelectedRows: [0, 1, 2, 3, 4],
            Category: TableTestCategory.Selection);
    }
    
    private static IEnumerable<TableVisualTestCase> GetAsyncCases()
    {
        yield return new TableVisualTestCase(
            Name: "50Rows_Compact_80x24_Loading",
            RowCount: 50,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            DataSourceType: TableDataSourceType.Async,
            Category: TableTestCategory.Async);
        
        yield return new TableVisualTestCase(
            Name: "50Rows_Full_80x24_Loading",
            RowCount: 50,
            Mode: TableRenderMode.Full,
            Width: 80,
            Height: 24,
            DataSourceType: TableDataSourceType.Async,
            Category: TableTestCategory.Async);
        
        yield return new TableVisualTestCase(
            Name: "1000Rows_Compact_80x24_PartialLoad",
            RowCount: 1000,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            DataSourceType: TableDataSourceType.Async,
            Category: TableTestCategory.Async);
        
        yield return new TableVisualTestCase(
            Name: "1000Rows_Full_160x48_Scrolled",
            RowCount: 1000,
            Mode: TableRenderMode.Full,
            Width: 160,
            Height: 48,
            DataSourceType: TableDataSourceType.Async,
            ScrollPosition: 500,
            Category: TableTestCategory.Async);
    }
    
    private static IEnumerable<TableVisualTestCase> GetFocusCases()
    {
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_Row0Focused",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            FocusedRow: 0,
            Category: TableTestCategory.Focus);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_Row2Focused",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            FocusedRow: 2,
            Category: TableTestCategory.Focus);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Full_Row4Focused",
            RowCount: 5,
            Mode: TableRenderMode.Full,
            Width: 80,
            Height: 24,
            FocusedRow: 4,
            Category: TableTestCategory.Focus);
        
        yield return new TableVisualTestCase(
            Name: "50Rows_Compact_Row25Focused_Scrolled",
            RowCount: 50,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            FocusedRow: 25,
            ScrollPosition: 20,
            Category: TableTestCategory.Focus);
        
        yield return new TableVisualTestCase(
            Name: "50Rows_Full_EndOfListFocused",
            RowCount: 50,
            Mode: TableRenderMode.Full,
            Width: 80,
            Height: 24,
            FocusedRow: 49,
            ScrollPosition: 40,
            Category: TableTestCategory.Focus);
    }
    
    private static IEnumerable<TableVisualTestCase> GetTableFocusCases()
    {
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_Unfocused",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            TableHasFocus: false,
            Category: TableTestCategory.TableFocus);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Compact_Focused",
            RowCount: 5,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            TableHasFocus: true,
            Category: TableTestCategory.TableFocus);
        
        yield return new TableVisualTestCase(
            Name: "5Rows_Full_Focused",
            RowCount: 5,
            Mode: TableRenderMode.Full,
            Width: 80,
            Height: 24,
            TableHasFocus: true,
            Category: TableTestCategory.TableFocus);
        
        yield return new TableVisualTestCase(
            Name: "50Rows_Compact_Focused_WithScrollbar",
            RowCount: 50,
            Mode: TableRenderMode.Compact,
            Width: 80,
            Height: 24,
            TableHasFocus: true,
            Category: TableTestCategory.TableFocus);
    }
}
