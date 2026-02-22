using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// Defines the sizing behavior for a grid column.
/// </summary>
/// <param name="Width">The size hint for this column's width. Defaults to <see cref="SizeHint.Content"/>.</param>
public sealed record GridColumnDefinition(SizeHint Width)
{
    /// <summary>
    /// Creates a column definition with content-based sizing.
    /// </summary>
    public GridColumnDefinition() : this(SizeHint.Content) { }
}
