using System.Reflection;
using System.Text;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a parsed FIGfont — a typeface used by <see cref="FigletTextWidget"/> to render
/// large ASCII-art text. Subclass to intercept glyph lookups, decorate or substitute glyphs,
/// or build a fully synthetic font without parsing a <c>.flf</c> file.
/// </summary>
/// <remarks>
/// <para>
/// Use one of the static factory methods to load a font:
/// <list type="bullet">
/// <item><description><see cref="LoadAsync(Stream, CancellationToken)"/> — load from any stream</description></item>
/// <item><description><see cref="LoadFileAsync(string, CancellationToken)"/> — load from disk</description></item>
/// <item><description><see cref="LoadBundledAsync(string, CancellationToken)"/> / <see cref="LoadBundled(string)"/> — load one of Hex1b's embedded fonts</description></item>
/// <item><description><see cref="Parse(string)"/> — parse a <c>.flf</c> string already in memory</description></item>
/// </list>
/// For the bundled fonts, prefer the lazy singletons exposed on <see cref="FigletFonts"/>
/// (e.g. <c>FigletFonts.Standard</c>) over re-loading the resource.
/// </para>
/// <para>
/// Subclasses can override <see cref="TryGetGlyph(int, out FigletGlyph)"/> to substitute
/// glyphs (for example to provide a fallback chain), and may use the
/// <see cref="FigletFont(FigletFont)"/> decorator constructor to wrap an existing font.
/// </para>
/// <para>
/// Instances are immutable and safe to share across threads. Parsing a font is moderately
/// expensive; bundled fonts are cached on <see cref="FigletFonts"/>.
/// </para>
/// </remarks>
public class FigletFont
{
    private readonly FigletFont? _inner;

    private readonly char _hardblank;
    private readonly int _height;
    private readonly int _baseline;
    private readonly int _horizontalSmushingRules;
    private readonly bool _horizontalSmushing;
    private readonly bool _horizontalFitting;
    private readonly int _verticalSmushingRules;
    private readonly bool _verticalSmushing;
    private readonly bool _verticalFitting;
    private readonly IReadOnlyDictionary<int, FigletGlyph>? _glyphs;

    /// <summary>
    /// Initializes a new <see cref="FigletFont"/> that delegates all behavior to <paramref name="inner"/>.
    /// Use this constructor when subclassing to decorate an existing font (for example to override
    /// <see cref="TryGetGlyph(int, out FigletGlyph)"/> for character substitution or fallback chains).
    /// </summary>
    /// <param name="inner">The font to delegate to. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="inner"/> is <see langword="null"/>.
    /// </exception>
    protected FigletFont(FigletFont inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>
    /// Initializes a new <see cref="FigletFont"/> from primitive layout parameters. Use this
    /// constructor when implementing a fully synthetic font (no <c>.flf</c> data).
    /// </summary>
    /// <param name="height">The number of rows in every glyph. Must be at least 1.</param>
    /// <param name="baseline">
    /// The 1-based row index of the FIGfont baseline counted from the top of a glyph.
    /// Must be in the range <c>[1, height]</c>.
    /// </param>
    /// <param name="hardblank">
    /// The sub-character that represents the hardblank in this font's glyph data. Renders as a
    /// space in output but participates in horizontal layout differently than ordinary spaces.
    /// </param>
    /// <param name="horizontalSmushingRules">
    /// Bitmask of horizontal smushing rules that this font opts in to (bits 1, 2, 4, 8, 16, 32).
    /// A value of <c>0</c> with <paramref name="horizontalSmushing"/> set enables universal smushing.
    /// </param>
    /// <param name="horizontalSmushing">True when smushing is the font's preferred horizontal layout.</param>
    /// <param name="horizontalFitting">True when fitting (kerning) is the font's preferred horizontal layout.</param>
    /// <param name="verticalSmushingRules">
    /// Bitmask of vertical smushing rules in the post-shift form (bits 1, 2, 4, 8, 16). A value of
    /// <c>0</c> with <paramref name="verticalSmushing"/> set enables universal vertical smushing.
    /// </param>
    /// <param name="verticalSmushing">True when smushing is the font's preferred vertical layout.</param>
    /// <param name="verticalFitting">True when fitting is the font's preferred vertical layout.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="height"/> is less than 1, or <paramref name="baseline"/> is
    /// outside <c>[1, height]</c>.
    /// </exception>
    protected FigletFont(
        int height,
        int baseline,
        char hardblank,
        int horizontalSmushingRules,
        bool horizontalSmushing,
        bool horizontalFitting,
        int verticalSmushingRules,
        bool verticalSmushing,
        bool verticalFitting)
    {
        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be at least 1.");
        }
        if (baseline < 1 || baseline > height)
        {
            throw new ArgumentOutOfRangeException(nameof(baseline), baseline, "Baseline must be in [1, height].");
        }

        _height = height;
        _baseline = baseline;
        _hardblank = hardblank;
        _horizontalSmushingRules = horizontalSmushingRules;
        _horizontalSmushing = horizontalSmushing;
        _horizontalFitting = horizontalFitting;
        _verticalSmushingRules = verticalSmushingRules;
        _verticalSmushing = verticalSmushing;
        _verticalFitting = verticalFitting;
    }

    // Internal ctor used by the parser. Subclasses go through the primitive ctor and supply
    // glyphs via TryGetGlyph; the parser's path stores its glyph dictionary directly.
    internal FigletFont(
        FigletFontHeader header,
        FigletLayoutInfo layout,
        IReadOnlyDictionary<int, FigletGlyph> glyphs)
        : this(
            height: header.Height,
            baseline: header.Baseline,
            hardblank: header.Hardblank,
            horizontalSmushingRules: layout.HorizontalSmushingRules,
            horizontalSmushing: layout.HorizontalSmushing,
            horizontalFitting: layout.HorizontalFitting,
            verticalSmushingRules: layout.VerticalSmushingRules,
            verticalSmushing: layout.VerticalSmushing,
            verticalFitting: layout.VerticalFitting)
    {
        _glyphs = glyphs;
    }

    // ----- Public virtual surface ----------------------------------------------------------

    /// <summary>
    /// Gets the height of every glyph in this font, in rows of sub-characters.
    /// </summary>
    public virtual int Height => _inner?.Height ?? _height;

    /// <summary>
    /// Gets the 1-based row index of the FIGfont baseline (counted from the top of a glyph).
    /// Capital letters rest on top of this row.
    /// </summary>
    public virtual int Baseline => _inner?.Baseline ?? _baseline;

    /// <summary>
    /// Gets the sub-character used to represent hardblanks in this font's glyph data.
    /// Hardblanks render as spaces but participate in horizontal layout differently.
    /// </summary>
    public virtual char Hardblank => _inner?.Hardblank ?? _hardblank;

    /// <summary>
    /// Looks up the glyph for <paramref name="codePoint"/>.
    /// </summary>
    /// <param name="codePoint">The Unicode code point of the desired character.</param>
    /// <param name="glyph">The glyph if found; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if a glyph exists in this font for the given code point; otherwise
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Override this method to substitute, decorate, or lazily generate glyphs. The default
    /// implementation consults the parsed glyph dictionary or, when constructed via
    /// <see cref="FigletFont(FigletFont)"/>, delegates to the inner font.
    /// </remarks>
    public virtual bool TryGetGlyph(int codePoint, out FigletGlyph glyph)
    {
        if (_inner is not null)
        {
            return _inner.TryGetGlyph(codePoint, out glyph);
        }
        if (_glyphs is not null && _glyphs.TryGetValue(codePoint, out var found))
        {
            glyph = found;
            return true;
        }
        glyph = null!;
        return false;
    }

    /// <summary>
    /// Returns the glyph used when <see cref="TryGetGlyph(int, out FigletGlyph)"/> returns
    /// <see langword="false"/>. The default implementation returns the font's space glyph if
    /// available, matching reference figlet behavior.
    /// </summary>
    /// <returns>The fallback glyph for missing characters.</returns>
    /// <remarks>
    /// Override to provide a custom missing-glyph appearance (for example a tofu box). If the
    /// font has no space glyph, the default implementation returns an empty glyph of the correct
    /// height.
    /// </remarks>
    public virtual FigletGlyph GetMissingGlyph()
    {
        if (_inner is not null)
        {
            return _inner.GetMissingGlyph();
        }
        if (TryGetGlyph(' ', out var space))
        {
            return space;
        }
        var emptyRows = new string[Height];
        for (var i = 0; i < emptyRows.Length; i++)
        {
            emptyRows[i] = string.Empty;
        }
        return new FigletGlyph(emptyRows);
    }

    // ----- Internal accessors used by the renderer -----------------------------------------

    internal int HorizontalSmushingRules => _inner?.HorizontalSmushingRules ?? _horizontalSmushingRules;
    internal bool HorizontalSmushing => _inner?.HorizontalSmushing ?? _horizontalSmushing;
    internal bool HorizontalFitting => _inner?.HorizontalFitting ?? _horizontalFitting;
    internal int VerticalSmushingRules => _inner?.VerticalSmushingRules ?? _verticalSmushingRules;
    internal bool VerticalSmushing => _inner?.VerticalSmushing ?? _verticalSmushing;
    internal bool VerticalFitting => _inner?.VerticalFitting ?? _verticalFitting;

    // ----- Static factories ----------------------------------------------------------------

    /// <summary>
    /// Asynchronously loads a FIGfont from a stream. The stream is read using ISO-8859-1
    /// (Latin-1) so that hardblanks and sub-characters in the upper Latin-1 range are preserved.
    /// </summary>
    /// <param name="stream">The stream to read. The caller retains ownership.</param>
    /// <param name="cancellationToken">A token to cancel the async copy.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="FigletFontFormatException">Thrown when the stream is not a valid FIGfont.</exception>
    public static async Task<FigletFont> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Buffer to memory so we can use the synchronous parser. .flf files are small (<100KB).
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        var data = FigletFontParser.Parse(buffer);
        return new FigletFont(data.Header, data.Layout, data.Glyphs);
    }

    /// <summary>
    /// Asynchronously loads a FIGfont from a file path.
    /// </summary>
    /// <param name="path">Path to a <c>.flf</c> file.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="FigletFontFormatException">Thrown when the file is not a valid FIGfont.</exception>
    public static async Task<FigletFont> LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously loads one of Hex1b's bundled FIGfonts by short name.
    /// </summary>
    /// <param name="name">
    /// The font name. Recognized values (case-insensitive): <c>standard</c>, <c>slant</c>,
    /// <c>small</c>, <c>big</c>, <c>mini</c>, <c>shadow</c>, <c>block</c>, <c>banner</c>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no bundled font matches <paramref name="name"/>.</exception>
    /// <remarks>
    /// For repeated access prefer the lazy singletons on <see cref="FigletFonts"/>; this method
    /// reloads and re-parses the resource on every call.
    /// </remarks>
    public static async Task<FigletFont> LoadBundledAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var stream = OpenBundledResource(name);
        return await LoadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronously loads one of Hex1b's bundled FIGfonts by short name. Used by
    /// <see cref="FigletFonts"/> to populate its lazy singletons.
    /// </summary>
    /// <param name="name">The bundled font's short name (see <see cref="LoadBundledAsync"/>).</param>
    /// <returns>The parsed font.</returns>
    public static FigletFont LoadBundled(string name)
    {
        using var stream = OpenBundledResource(name);
        var data = FigletFontParser.Parse(stream);
        return new FigletFont(data.Header, data.Layout, data.Glyphs);
    }

    /// <summary>
    /// Parses a FIGfont from an in-memory <c>.flf</c> string.
    /// </summary>
    /// <param name="flfContent">The full text of a <c>.flf</c> file.</param>
    /// <returns>The parsed font.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="flfContent"/> is null.</exception>
    /// <exception cref="FigletFontFormatException">Thrown when the content is not a valid FIGfont.</exception>
    public static FigletFont Parse(string flfContent)
    {
        var data = FigletFontParser.Parse(flfContent);
        return new FigletFont(data.Header, data.Layout, data.Glyphs);
    }

    private static Stream OpenBundledResource(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var canonical = name.Trim().ToLowerInvariant();
        var resourceName = $"Hex1b.Widgets.FigletFonts.{canonical}.flf";

        var asm = typeof(FigletFont).Assembly;
        var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new ArgumentException(
                $"No bundled FIGfont named '{name}'. Recognized names: standard, slant, small, big, mini, shadow, block, banner.",
                nameof(name));

        return stream;
    }

    /// <summary>
    /// Returns a string identifying the font (height, hardblank, layout flags). Useful for diagnostics.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("FigletFont(height=").Append(Height)
          .Append(", baseline=").Append(Baseline)
          .Append(", hardblank='").Append(Hardblank).Append('\'');
        if (HorizontalSmushing) sb.Append(", hSmush");
        if (HorizontalFitting) sb.Append(", hFit");
        if (HorizontalSmushingRules != 0) sb.Append(", hRules=").Append(HorizontalSmushingRules);
        if (VerticalSmushing) sb.Append(", vSmush");
        if (VerticalFitting) sb.Append(", vFit");
        if (VerticalSmushingRules != 0) sb.Append(", vRules=").Append(VerticalSmushingRules);
        sb.Append(')');
        return sb.ToString();
    }
}
