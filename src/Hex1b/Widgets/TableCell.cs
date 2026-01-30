using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a cell definition in a table, containing either text or a widget builder.
/// </summary>
public record TableCell
{
    /// <summary>
    /// The text content of the cell. Mutually exclusive with WidgetBuilder.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// A builder function to create a widget for this cell. Mutually exclusive with Text.
    /// </summary>
    public Func<TableCellContext, Hex1bWidget>? WidgetBuilder { get; init; }

    /// <summary>
    /// The width hint for this column. Only respected when set on header cells.
    /// </summary>
    public SizeHint? Width { get; init; }

    /// <summary>
    /// The horizontal alignment for cell content. Only respected when set on header cells.
    /// </summary>
    public Alignment Alignment { get; init; } = Alignment.Left;

    /// <summary>
    /// Creates a text cell.
    /// </summary>
    public static TableCell FromText(string text) => new() { Text = text };

    /// <summary>
    /// Creates a widget cell.
    /// </summary>
    public static TableCell FromWidget(Func<TableCellContext, Hex1bWidget> builder) 
        => new() { WidgetBuilder = builder };
}

/// <summary>
/// Context provided when building a widget cell.
/// </summary>
public class TableCellContext : RootContext
{
}
