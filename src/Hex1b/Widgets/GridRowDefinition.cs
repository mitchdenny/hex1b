using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// Defines the sizing behavior for a grid row.
/// </summary>
/// <param name="Height">The size hint for this row's height. Defaults to <see cref="SizeHint.Content"/>.</param>
public sealed record GridRowDefinition(SizeHint Height)
{
    /// <summary>
    /// Creates a row definition with content-based sizing.
    /// </summary>
    public GridRowDefinition() : this(SizeHint.Content) { }
}
