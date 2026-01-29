using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// Context for building table header cells.
/// </summary>
public class TableHeaderContext
{
    /// <summary>
    /// Creates a text cell for the header.
    /// </summary>
    public TableCell Cell(string text) => new() { Text = text };

    /// <summary>
    /// Creates a widget cell for the header.
    /// </summary>
    public TableCell Cell(Func<TableCellContext, Hex1bWidget> builder) 
        => new() { WidgetBuilder = builder };
}

/// <summary>
/// Context for building table row cells.
/// </summary>
public class TableRowContext
{
    /// <summary>
    /// Creates a text cell for the row.
    /// </summary>
    public TableCell Cell(string text) => new() { Text = text };

    /// <summary>
    /// Creates a widget cell for the row.
    /// </summary>
    public TableCell Cell(Func<TableCellContext, Hex1bWidget> builder) 
        => new() { WidgetBuilder = builder };
}

/// <summary>
/// Context for building table footer cells.
/// </summary>
public class TableFooterContext
{
    /// <summary>
    /// Creates a text cell for the footer.
    /// </summary>
    public TableCell Cell(string text) => new() { Text = text };

    /// <summary>
    /// Creates a widget cell for the footer.
    /// </summary>
    public TableCell Cell(Func<TableCellContext, Hex1bWidget> builder) 
        => new() { WidgetBuilder = builder };
}

/// <summary>
/// Extension methods for TableCell to enable fluent column configuration.
/// </summary>
public static class TableCellExtensions
{
    /// <summary>
    /// Sets the width hint for this column.
    /// </summary>
    public static TableCell Width(this TableCell cell, SizeHint width) 
        => cell with { Width = width };

    /// <summary>
    /// Sets the alignment for this column.
    /// </summary>
    public static TableCell Align(this TableCell cell, Alignment alignment) 
        => cell with { Alignment = alignment };

    /// <summary>
    /// Sets the column to auto-size based on content.
    /// </summary>
    public static TableCell Auto(this TableCell cell) 
        => cell with { Width = SizeHint.Content };

    /// <summary>
    /// Sets the column to a fixed width.
    /// </summary>
    public static TableCell Fixed(this TableCell cell, int width) 
        => cell with { Width = SizeHint.Fixed(width) };

    /// <summary>
    /// Sets the column to fill remaining space.
    /// </summary>
    public static TableCell Fill(this TableCell cell, int weight = 1) 
        => cell with { Width = weight == 1 ? SizeHint.Fill : SizeHint.Weighted(weight) };

    /// <summary>
    /// Sets right alignment for this column.
    /// </summary>
    public static TableCell AlignRight(this TableCell cell) 
        => cell with { Alignment = Alignment.Right };

    /// <summary>
    /// Sets center alignment for this column.
    /// </summary>
    public static TableCell AlignCenter(this TableCell cell) 
        => cell with { Alignment = Alignment.Center };
}
