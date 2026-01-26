using Hex1b.Theming;

namespace Hex1b.Surfaces;

/// <summary>
/// Represents a single cell in a surface - the atomic unit of terminal rendering.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="SurfaceCell"/> represents a single character position in a terminal surface,
/// including the character to display, its colors, styling attributes, and optional
/// extensions like hyperlinks and sixel graphics.
/// </para>
/// <para>
/// For wide characters (CJK, emoji, etc.) that occupy multiple terminal columns,
/// the primary cell contains the character with <see cref="DisplayWidth"/> set to 2 (or more),
/// and subsequent cells are continuation cells with <see cref="DisplayWidth"/> set to 0.
/// </para>
/// </remarks>
/// <param name="Character">The grapheme cluster (user-perceived character) to display. May be multiple code points for emoji, combining characters, etc.</param>
/// <param name="Foreground">The foreground color, or null for transparent (shows through from layer below).</param>
/// <param name="Background">The background color, or null for transparent (shows through from layer below).</param>
/// <param name="Attributes">Text styling attributes (bold, italic, underline, etc.).</param>
/// <param name="DisplayWidth">The number of terminal columns this cell occupies. 1 for normal, 2 for wide, 0 for continuation.</param>
/// <param name="Sixel">Optional tracked reference to Sixel graphics data.</param>
/// <param name="Hyperlink">Optional tracked reference to hyperlink data.</param>
public readonly record struct SurfaceCell(
    string Character,
    Hex1bColor? Foreground,
    Hex1bColor? Background,
    CellAttributes Attributes = CellAttributes.None,
    int DisplayWidth = 1,
    TrackedObject<SixelData>? Sixel = null,
    TrackedObject<HyperlinkData>? Hyperlink = null)
{
    /// <summary>
    /// Gets whether this cell is a continuation of a previous wide character.
    /// Continuation cells should not be rendered directly - the wide character in the previous cell covers this position.
    /// </summary>
    public bool IsContinuation => DisplayWidth == 0;

    /// <summary>
    /// Gets whether this cell contains a wide character (occupies 2+ columns).
    /// </summary>
    public bool IsWide => DisplayWidth >= 2;

    /// <summary>
    /// Gets whether this cell is fully transparent (both foreground and background are null).
    /// </summary>
    public bool IsTransparent => Foreground is null && Background is null;

    /// <summary>
    /// Gets whether this cell has a transparent background.
    /// When compositing, the background from the layer below will show through.
    /// </summary>
    public bool HasTransparentBackground => Background is null;

    /// <summary>
    /// Gets whether this cell has a transparent foreground.
    /// When compositing, the foreground from the layer below will show through.
    /// </summary>
    public bool HasTransparentForeground => Foreground is null;

    /// <summary>
    /// Gets whether this cell contains Sixel graphics data.
    /// </summary>
    public bool HasSixel => Sixel is not null;

    /// <summary>
    /// Gets whether this cell contains hyperlink data.
    /// </summary>
    public bool HasHyperlink => Hyperlink is not null;

    /// <summary>
    /// Creates a new cell with the specified foreground color.
    /// </summary>
    public SurfaceCell WithForeground(Hex1bColor? foreground)
        => this with { Foreground = foreground };

    /// <summary>
    /// Creates a new cell with the specified background color.
    /// </summary>
    public SurfaceCell WithBackground(Hex1bColor? background)
        => this with { Background = background };

    /// <summary>
    /// Creates a new cell with the specified attributes.
    /// </summary>
    public SurfaceCell WithAttributes(CellAttributes attributes)
        => this with { Attributes = attributes };

    /// <summary>
    /// Creates a new cell with additional attributes added.
    /// </summary>
    public SurfaceCell WithAddedAttributes(CellAttributes attributes)
        => this with { Attributes = Attributes | attributes };

    /// <summary>
    /// Creates a continuation cell for wide characters.
    /// This is used internally when a wide character occupies multiple columns.
    /// </summary>
    internal static SurfaceCell CreateContinuation(Hex1bColor? background)
        => new(string.Empty, null, background, CellAttributes.None, 0);
}
