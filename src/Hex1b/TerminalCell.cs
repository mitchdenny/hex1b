using Hex1b.Terminal;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Represents a single cell in the virtual terminal screen buffer.
/// </summary>
/// <param name="Character">The grapheme cluster (user-perceived character) displayed in this cell. May be multiple code points for emoji, combining characters, etc.</param>
/// <param name="Foreground">The foreground color, or null for default.</param>
/// <param name="Background">The background color, or null for default.</param>
/// <param name="Attributes">Text styling attributes (bold, italic, etc.).</param>
/// <param name="Sequence">The write order of this cell. Higher values were written later. Used for z-ordering during rendering.</param>
/// <param name="WrittenAt">The timestamp when this cell was written. Useful for debugging and future animation features.</param>
/// <param name="TrackedSixel">Optional tracked reference to Sixel graphics data associated with this cell.</param>
/// <param name="TrackedHyperlink">Optional tracked reference to hyperlink data associated with this cell.</param>
public readonly record struct TerminalCell(
    string Character,
    Hex1bColor? Foreground,
    Hex1bColor? Background,
    CellAttributes Attributes = CellAttributes.None,
    long Sequence = 0,
    DateTimeOffset WrittenAt = default,
    TrackedObject<SixelData>? TrackedSixel = null,
    TrackedObject<HyperlinkData>? TrackedHyperlink = null)
{
    /// <summary>An empty cell with default attributes.</summary>
    public static readonly TerminalCell Empty = new(" ", null, null, CellAttributes.None, 0, default);

    /// <summary>Gets whether this cell has bold text.</summary>
    public bool IsBold => (Attributes & CellAttributes.Bold) != 0;

    /// <summary>Gets whether this cell has dim/faint text.</summary>
    public bool IsDim => (Attributes & CellAttributes.Dim) != 0;

    /// <summary>Gets whether this cell has italic text.</summary>
    public bool IsItalic => (Attributes & CellAttributes.Italic) != 0;

    /// <summary>Gets whether this cell has underlined text.</summary>
    public bool IsUnderline => (Attributes & CellAttributes.Underline) != 0;

    /// <summary>Gets whether this cell has blinking text.</summary>
    public bool IsBlink => (Attributes & CellAttributes.Blink) != 0;

    /// <summary>Gets whether this cell has reverse video (inverted colors).</summary>
    public bool IsReverse => (Attributes & CellAttributes.Reverse) != 0;

    /// <summary>Gets whether this cell has hidden/invisible text.</summary>
    public bool IsHidden => (Attributes & CellAttributes.Hidden) != 0;

    /// <summary>Gets whether this cell has strikethrough text.</summary>
    public bool IsStrikethrough => (Attributes & CellAttributes.Strikethrough) != 0;

    /// <summary>Gets whether this cell has overlined text.</summary>
    public bool IsOverline => (Attributes & CellAttributes.Overline) != 0;

    /// <summary>Gets whether this cell contains Sixel graphics.</summary>
    public bool IsSixel => (Attributes & CellAttributes.Sixel) != 0;

    /// <summary>Gets the Sixel data if this cell has any, otherwise null.</summary>
    public SixelData? SixelData => TrackedSixel?.Data;

    /// <summary>Gets whether this cell has associated Sixel data.</summary>
    public bool HasSixelData => TrackedSixel is not null;

    /// <summary>Gets the hyperlink data if this cell has any, otherwise null.</summary>
    public HyperlinkData? HyperlinkData => TrackedHyperlink?.Data;

    /// <summary>Gets whether this cell has associated hyperlink data.</summary>
    public bool HasHyperlinkData => TrackedHyperlink is not null;
}
