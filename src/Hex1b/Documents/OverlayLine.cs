using Hex1b.Documents;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A single line of styled text in an editor overlay.
/// </summary>
public record OverlayLine(string Text, Hex1bColor? Foreground = null, Hex1bColor? Background = null)
{
    /// <summary>
    /// Rich styled segments for this line. When set, these are used instead
    /// of Text/Foreground/Background for rendering.
    /// </summary>
    public IReadOnlyList<OverlaySegment>? Segments { get; init; }
}
