using Hex1b.Layout;

namespace Hex1b.Widgets;

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
