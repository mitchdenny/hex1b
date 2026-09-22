using Hex1b.Theming;

namespace Hex1b.Markdown;

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
