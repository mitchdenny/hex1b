namespace Hex1b.Widgets;

/// <summary>
/// Static catalog of FIGfonts bundled with Hex1b. Each property exposes a lazily-loaded singleton
/// instance; the underlying <c>.flf</c> resource is parsed on first access and cached for the
/// lifetime of the process.
/// </summary>
/// <remarks>
/// <para>
/// All bundled fonts are part of the standard FIGlet 2.2 distribution and are redistributed
/// under the FIGlet 3-clause BSD license (see <c>THIRD-PARTY-NOTICES.txt</c> in the package).
/// Their original author attributions are preserved verbatim inside each <c>.flf</c> file's
/// header comment block.
/// </para>
/// <para>
/// To use a custom font, call <see cref="FigletFont.LoadFileAsync(string, System.Threading.CancellationToken)"/>
/// or <see cref="FigletFont.LoadAsync(System.IO.Stream, System.Threading.CancellationToken)"/>
/// at startup and pass the resulting font to
/// <see cref="FigletTextExtensions.Font(FigletTextWidget, FigletFont)"/>.
/// </para>
/// </remarks>
public static class FigletFonts
{
    private static readonly Lazy<FigletFont> s_standard = new(() => FigletFont.LoadBundled("standard"));
    private static readonly Lazy<FigletFont> s_slant    = new(() => FigletFont.LoadBundled("slant"));
    private static readonly Lazy<FigletFont> s_small    = new(() => FigletFont.LoadBundled("small"));
    private static readonly Lazy<FigletFont> s_big      = new(() => FigletFont.LoadBundled("big"));
    private static readonly Lazy<FigletFont> s_mini     = new(() => FigletFont.LoadBundled("mini"));
    private static readonly Lazy<FigletFont> s_shadow   = new(() => FigletFont.LoadBundled("shadow"));
    private static readonly Lazy<FigletFont> s_block    = new(() => FigletFont.LoadBundled("block"));
    private static readonly Lazy<FigletFont> s_banner   = new(() => FigletFont.LoadBundled("banner"));

    /// <summary>
    /// The "standard" FIGfont — the default monospace banner font from the FIGlet distribution.
    /// </summary>
    public static FigletFont Standard => s_standard.Value;

    /// <summary>The "slant" FIGfont — italicized variant of the standard font.</summary>
    public static FigletFont Slant => s_slant.Value;

    /// <summary>The "small" FIGfont — a compact 5-row variant suitable for narrower terminals.</summary>
    public static FigletFont Small => s_small.Value;

    /// <summary>The "big" FIGfont — an enlarged 8-row banner font.</summary>
    public static FigletFont Big => s_big.Value;

    /// <summary>The "mini" FIGfont — a tiny 4-row font for extremely narrow output.</summary>
    public static FigletFont Mini => s_mini.Value;

    /// <summary>The "shadow" FIGfont — text with a drop-shadow effect.</summary>
    public static FigletFont Shadow => s_shadow.Value;

    /// <summary>The "block" FIGfont — solid block letters built from <c>_/[]</c>.</summary>
    public static FigletFont Block => s_block.Value;

    /// <summary>The "banner" FIGfont — wide, hash-shaped letters reminiscent of <c>banner(1)</c>.</summary>
    public static FigletFont Banner => s_banner.Value;

    /// <summary>
    /// All bundled fonts in declaration order. Accessing this property forces every font to be
    /// loaded; use it for demos and font pickers, but prefer the per-font properties for
    /// production code that only needs one font.
    /// </summary>
    public static IReadOnlyList<FigletFont> All =>
    [
        Standard, Slant, Small, Big, Mini, Shadow, Block, Banner,
    ];

    /// <summary>
    /// All bundled font names in declaration order. Useful for building font pickers without
    /// triggering load of every font.
    /// </summary>
    public static IReadOnlyList<string> Names =>
    [
        "standard", "slant", "small", "big", "mini", "shadow", "block", "banner",
    ];
}
