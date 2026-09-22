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
