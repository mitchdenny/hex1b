using Hex1b.Theming;

namespace Hex1b.Surfaces;

/// <summary>
/// Provides commonly used <see cref="SurfaceCell"/> values.
/// </summary>
/// <remarks>
/// Using these static instances avoids repeated allocations for common cell types.
/// Since <see cref="SurfaceCell"/> is a value type (record struct), these don't save memory
/// directly, but they provide convenient constants and enable reference equality checks
/// in certain optimization scenarios.
/// </remarks>
public static class SurfaceCells
{
    /// <summary>
    /// An empty cell with a space character and no colors (fully transparent).
    /// This is the default cell used to initialize surfaces.
    /// </summary>
    public static readonly SurfaceCell Empty = new(" ", null, null, CellAttributes.None, 1);

    /// <summary>
    /// A continuation cell used for wide character support.
    /// When a wide character (like emoji or CJK) spans multiple columns,
    /// subsequent columns contain this cell to indicate they are covered
    /// by the preceding wide character.
    /// </summary>
    public static readonly SurfaceCell Continuation = new(string.Empty, null, null, CellAttributes.None, 0);

    /// <summary>
    /// Creates a cell containing a single character with optional styling.
    /// </summary>
    /// <param name="character">The character to display.</param>
    /// <param name="foreground">The foreground color, or null for transparent.</param>
    /// <param name="background">The background color, or null for transparent.</param>
    /// <param name="attributes">Text styling attributes.</param>
    /// <returns>A new <see cref="SurfaceCell"/> with the specified properties.</returns>
    public static SurfaceCell Char(char character, Hex1bColor? foreground = null, Hex1bColor? background = null, CellAttributes attributes = CellAttributes.None)
        => new(character.ToString(), foreground, background, attributes, 1);

    /// <summary>
    /// Creates a cell containing a grapheme cluster with optional styling.
    /// </summary>
    /// <param name="grapheme">The grapheme cluster (may be multiple code points for emoji, combining chars, etc.).</param>
    /// <param name="foreground">The foreground color, or null for transparent.</param>
    /// <param name="background">The background color, or null for transparent.</param>
    /// <param name="attributes">Text styling attributes.</param>
    /// <param name="displayWidth">The display width of the grapheme (1 for normal, 2 for wide).</param>
    /// <returns>A new <see cref="SurfaceCell"/> with the specified properties.</returns>
    public static SurfaceCell Grapheme(string grapheme, Hex1bColor? foreground = null, Hex1bColor? background = null, CellAttributes attributes = CellAttributes.None, int displayWidth = 1)
        => new(grapheme, foreground, background, attributes, displayWidth);

    /// <summary>
    /// Creates a space cell with the specified background color.
    /// Useful for clearing regions with a specific background.
    /// </summary>
    /// <param name="background">The background color.</param>
    /// <returns>A space cell with the specified background.</returns>
    public static SurfaceCell Space(Hex1bColor? background)
        => new(" ", null, background, CellAttributes.None, 1);

    /// <summary>
    /// Creates a space cell with the specified foreground and background colors.
    /// </summary>
    /// <param name="foreground">The foreground color.</param>
    /// <param name="background">The background color.</param>
    /// <returns>A space cell with the specified colors.</returns>
    public static SurfaceCell Space(Hex1bColor? foreground, Hex1bColor? background)
        => new(" ", foreground, background, CellAttributes.None, 1);
}
