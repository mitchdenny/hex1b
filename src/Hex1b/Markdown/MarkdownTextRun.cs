using Hex1b.Theming;

namespace Hex1b.Markdown;

/// <summary>
/// A styled run of text — a contiguous piece of text with consistent styling.
/// Used both as the intermediate representation from flattening the inline AST,
/// and as fragments within a <see cref="StyledWord"/>.
/// </summary>
internal readonly record struct MarkdownTextRun(
    string Text,
    Hex1bColor? Foreground,
    Hex1bColor? Background,
    CellAttributes Attributes,
    string? Url = null,
    int LinkId = -1);

/// <summary>
/// A word (or non-breakable unit) composed of one or more styled fragments.
/// This is the atomic unit for line wrapping — wrapping decisions are made at
/// word boundaries (spaces), but a single word may contain multiple styles
/// (e.g., <c>par**tial**ly</c> is one word with three fragments).
/// </summary>
internal readonly record struct StyledWord(
    IReadOnlyList<MarkdownTextRun> Fragments,
    int DisplayWidth,
    bool PrecededBySpace);

/// <summary>
/// Describes the position and metadata of a link region within wrapped text.
/// Used by <see cref="Hex1b.Nodes.MarkdownTextBlockNode"/> to create and
/// position <see cref="Hex1b.Nodes.MarkdownLinkRegionNode"/> children.
/// </summary>
internal readonly record struct LinkRegionInfo(
    int LinkId,
    string Url,
    string Text,
    int LineIndex,
    int ColumnOffset,
    int DisplayWidth);

/// <summary>
/// Result of wrapping styled words into lines. Contains the rendered ANSI lines
/// along with link position metadata.
/// </summary>
internal readonly record struct WrapResult(
    IReadOnlyList<string> Lines,
    IReadOnlyList<LinkRegionInfo> LinkRegions);

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
