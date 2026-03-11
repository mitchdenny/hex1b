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
    string? Url = null);

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
