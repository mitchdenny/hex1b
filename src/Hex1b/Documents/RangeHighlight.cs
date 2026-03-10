using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// A temporary background highlight applied to a document range.
/// Used for search results, symbol occurrences, definition flashes, etc.
/// </summary>
/// <param name="Start">Start position of the highlight (1-based line/column).</param>
/// <param name="End">End position (exclusive) of the highlight (1-based line/column).</param>
/// <param name="Kind">The kind of highlight, which determines the default background color.</param>
public record RangeHighlight(
    DocumentPosition Start,
    DocumentPosition End,
    RangeHighlightKind Kind = RangeHighlightKind.Default)
{
    /// <summary>
    /// Optional explicit background color. When set, overrides the theme default for <see cref="Kind"/>.
    /// </summary>
    public Hex1bColor? Background { get; init; }
}

/// <summary>
/// Predefined highlight kinds with distinct theme-aware default colors.
/// </summary>
public enum RangeHighlightKind
{
    /// <summary>General-purpose highlight (e.g., search results).</summary>
    Default,

    /// <summary>Read access of a symbol (e.g., variable reference).</summary>
    ReadAccess,

    /// <summary>Write access of a symbol (e.g., variable assignment).</summary>
    WriteAccess
}
