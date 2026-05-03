namespace Hex1b.Widgets;

/// <summary>
/// Represents a single FIGcharacter (large ASCII-art glyph) in a FIGfont.
/// </summary>
/// <remarks>
/// <para>
/// A glyph is a fixed-height block of sub-characters. Every glyph in a given <see cref="FigletFont"/>
/// has the same height (the font's <see cref="FigletFont.Height"/>) but rows may have different widths.
/// Glyphs are obtained from a font through <see cref="FigletFont.TryGetGlyph(int, out FigletGlyph)"/>.
/// </para>
/// <para>
/// Sub-characters in the rows include normal printable characters along with the font's
/// <see cref="FigletFont.Hardblank"/> character. Hardblanks render as spaces but participate
/// in horizontal layout differently than ordinary spaces — see the FIGfont 2.0 specification.
/// </para>
/// <para>
/// The <see cref="Width"/> property reports the maximum number of code units across all rows.
/// Individual rows may be shorter; renderers should treat shorter rows as if padded with spaces.
/// </para>
/// </remarks>
public sealed class FigletGlyph
{
    private readonly string[] _rows;

    /// <summary>
    /// Initializes a new instance of the <see cref="FigletGlyph"/> class.
    /// </summary>
    /// <param name="rows">The glyph rows (one string per row of sub-characters).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rows"/> is <see langword="null"/>.
    /// </exception>
    public FigletGlyph(IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _rows = new string[rows.Count];
        var maxWidth = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? string.Empty;
            _rows[i] = row;
            if (row.Length > maxWidth)
            {
                maxWidth = row.Length;
            }
        }

        Width = maxWidth;
    }

    /// <summary>
    /// Gets the height of this glyph in rows. Always equals the parent font's
    /// <see cref="FigletFont.Height"/>.
    /// </summary>
    public int Height => _rows.Length;

    /// <summary>
    /// Gets the maximum row width across this glyph in code units. Individual rows may be shorter
    /// and should be treated as if padded with spaces by renderers.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the rows of sub-characters that make up this glyph.
    /// </summary>
    /// <remarks>
    /// The returned list has exactly <see cref="Height"/> entries. Each row may include the parent
    /// font's hardblank character (see <see cref="FigletFont.Hardblank"/>), which renders as a space
    /// after layout completes.
    /// </remarks>
    public IReadOnlyList<string> Rows => _rows;

    /// <summary>
    /// Gets the row at the specified index.
    /// </summary>
    /// <param name="row">The zero-based row index.</param>
    /// <returns>The row string. May be shorter than <see cref="Width"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="row"/> is outside the range <c>[0, Height)</c>.
    /// </exception>
    public string GetRow(int row)
    {
        if ((uint)row >= (uint)_rows.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        return _rows[row];
    }
}
