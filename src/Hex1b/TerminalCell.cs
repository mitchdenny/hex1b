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
/// <param name="TrackedHyperlink">Optional tracked reference to hyperlink data associated with this cell.</param>
/// <param name="UnderlineColor">The underline color (SGR 58), or null to use the foreground color.</param>
/// <param name="UnderlineStyle">The underline style (SGR 4:x). Defaults to <see cref="Hex1b.UnderlineStyle.None"/>.</param>
/// <remarks>
/// Sixel graphics are no longer owned by a cell. A cell that happens to sit
/// under a Sixel placement carries no marker or reference at all; placement
/// occupancy and image lifetime are tracked independently by the terminal's
/// Sixel graphics state (see <c>SixelGraphicsState</c>) so overwriting a cell
/// never has side effects on Sixel image lifetime.
/// </remarks>
public readonly record struct TerminalCell(
    string Character,
    Hex1bColor? Foreground,
    Hex1bColor? Background,
    CellAttributes Attributes = CellAttributes.None,
    long Sequence = 0,
    DateTimeOffset WrittenAt = default,
    TrackedObject<HyperlinkData>? TrackedHyperlink = null,
    Hex1bColor? UnderlineColor = null,
    UnderlineStyle UnderlineStyle = UnderlineStyle.None)
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

    /// <summary>Gets whether this cell is a soft-wrap point (content continues on the next row).</summary>
    public bool IsSoftWrap => (Attributes & CellAttributes.SoftWrap) != 0;

    /// <summary>Gets the hyperlink data if this cell has any, otherwise null.</summary>
    public HyperlinkData? HyperlinkData => TrackedHyperlink?.Data;

    /// <summary>Gets whether this cell has associated hyperlink data.</summary>
    public bool HasHyperlinkData => TrackedHyperlink is not null;
}
