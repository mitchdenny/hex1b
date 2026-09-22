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
