using Hex1b.Documents;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A styled text segment within an overlay line. Allows mixing colors
/// and styles within a single line.
/// </summary>
public record OverlaySegment(
    string Text,
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null)
{
    /// <summary>Whether this segment should be rendered in bold.</summary>
    public bool IsBold { get; init; }

    /// <summary>Whether this segment should be rendered in italic.</summary>
    public bool IsItalic { get; init; }
}
