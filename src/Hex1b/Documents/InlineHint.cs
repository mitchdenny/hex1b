using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Virtual text rendered inline at a document position without modifying the document.
/// Hints are inserted before the character at the specified position, shifting
/// subsequent text to the right visually but not in the document model.
/// </summary>
/// <param name="Position">Document position (1-based line/column) where the hint appears.</param>
/// <param name="Text">The virtual text to display.</param>
/// <param name="Decoration">Optional styling for the hint text. If null, theme defaults are used.</param>
public record InlineHint(
    DocumentPosition Position,
    string Text,
    TextDecoration? Decoration = null);
