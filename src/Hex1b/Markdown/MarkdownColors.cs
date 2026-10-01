using Hex1b.Theming;

namespace Hex1b.Markdown;

/// <summary>
/// Resolved theme colors for inline rendering. Passed through to the
/// <see cref="MarkdownInlineRenderer"/> so that hardcoded color values
/// can be replaced with theme-aware values.
/// </summary>
internal readonly record struct MarkdownColors(
    Hex1bColor LinkForeground,
    Hex1bColor InlineCodeForeground,
    Hex1bColor InlineCodeBackground,
    Hex1bColor FocusedLinkForeground,
    Hex1bColor FocusedLinkBackground)
{
    /// <summary>
    /// Default colors matching the original hardcoded values.
    /// </summary>
    public static MarkdownColors Default { get; } = new(
        LinkForeground: Hex1bColor.FromRgb(100, 160, 255),
        InlineCodeForeground: Hex1bColor.FromRgb(220, 170, 120),
        InlineCodeBackground: Hex1bColor.FromRgb(50, 50, 50),
        FocusedLinkForeground: Hex1bColor.FromRgb(0, 0, 0),
        FocusedLinkBackground: Hex1bColor.FromRgb(100, 160, 255));
}
