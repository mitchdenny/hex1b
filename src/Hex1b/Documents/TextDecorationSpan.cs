namespace Hex1b.Documents;

/// <summary>
/// A <see cref="TextDecoration"/> applied to a range of document positions.
/// Multiple spans may overlap; the <see cref="Priority"/> determines which decoration
/// attributes win when they conflict. Higher priority values take precedence.
/// </summary>
/// <param name="Start">Start position (1-based line/column, inclusive).</param>
/// <param name="End">End position (1-based line/column, exclusive).</param>
/// <param name="Decoration">The visual decoration to apply.</param>
/// <param name="Priority">
/// Resolution priority for overlapping decorations. Higher values win.
/// Cursor and selection styling always take precedence regardless of priority.
/// </param>
public record TextDecorationSpan(
    DocumentPosition Start,
    DocumentPosition End,
    TextDecoration Decoration,
    int Priority = 0);
