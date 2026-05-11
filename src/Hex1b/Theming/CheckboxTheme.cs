namespace Hex1b.Theming;

/// <summary>
/// Theme elements for CheckboxWidget.
/// </summary>
/// <remarks>
/// As of the styling refresh, the box renders as a single Unicode glyph
/// surrounded by a "chip" background (defaults to a grey just lighter than
/// the input-field family) so the control reads as solid rather than the
/// older <c>[x]</c> / <c>[ ]</c> bracket pair. The trailing label still
/// renders against the global background.
/// </remarks>
public static class CheckboxTheme
{
    #region Colors

    /// <summary>
    /// Foreground colour for the trailing label and for the box glyph when the
    /// state-specific colour (<see cref="CheckMarkColor"/> /
    /// <see cref="IndeterminateColor"/>) is left at <see cref="Hex1bColor.Default"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Background colour for the trailing label. Defaults to
    /// <see cref="Hex1bColor.Default"/> so the label sits on the surrounding
    /// surface without an extra band — the chip is intentionally limited to
    /// the box glyph itself.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Resting chip background painted behind the box glyph (the
    /// <c>" □ "</c> / <c>" ▣ "</c> / <c>" ▤ "</c> cells). Defaults to the
    /// same <c>rgb(60,60,60)</c> as <see cref="ButtonTheme.BackgroundColor"/>
    /// so checkboxes and buttons share a coherent "solid" feel. Set to
    /// <see cref="Hex1bColor.Default"/> to disable the chip and render the
    /// box glyph against the surrounding surface.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BoxBackgroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(BoxBackgroundColor)}", () => Hex1bColor.FromRgb(60, 60, 60));

    /// <summary>
    /// Foreground for the entire control (box + label) when the checkbox is
    /// focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background for the entire control (box + label) when the checkbox is
    /// focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Foreground for the entire control (box + label) when the checkbox is
    /// hovered (mouse over) but not focused. Mirrors
    /// <see cref="ButtonTheme.HoveredForegroundColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background for the entire control (box + label) when the checkbox is
    /// hovered (mouse over) but not focused. Mirrors
    /// <see cref="ButtonTheme.HoveredBackgroundColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredBackgroundColor =
        new($"{nameof(CheckboxTheme)}.{nameof(HoveredBackgroundColor)}", () => Hex1bColor.FromRgb(180, 180, 180));

    /// <summary>
    /// Foreground colour for the box glyph when the checkbox is checked
    /// (resting state — focus / hover overrides win). Defaults to
    /// <see cref="Hex1bColor.Green"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CheckMarkColor =
        new($"{nameof(CheckboxTheme)}.{nameof(CheckMarkColor)}", () => Hex1bColor.Green);

    /// <summary>
    /// Foreground colour for the box glyph when the checkbox is in the
    /// indeterminate state (resting state — focus / hover overrides win).
    /// Defaults to <see cref="Hex1bColor.Yellow"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> IndeterminateColor =
        new($"{nameof(CheckboxTheme)}.{nameof(IndeterminateColor)}", () => Hex1bColor.Yellow);

    #endregion

    #region Characters

    /// <summary>
    /// The checkbox glyph (padded to 3 display cells) used when the checkbox
    /// is checked. Default is <c>" ▣ "</c> — a 1-cell U+25A3 "white square
    /// containing black small square" framed by single-cell padding so it
    /// fills a 3-cell chip.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CheckedBox =
        new($"{nameof(CheckboxTheme)}.{nameof(CheckedBox)}", () => " ▣ ");

    /// <summary>
    /// The checkbox glyph (padded to 3 display cells) used when the checkbox
    /// is unchecked. Default is <c>" □ "</c> — a 1-cell U+25A1 "white square"
    /// framed by single-cell padding so it fills a 3-cell chip.
    /// </summary>
    public static readonly Hex1bThemeElement<string> UncheckedBox =
        new($"{nameof(CheckboxTheme)}.{nameof(UncheckedBox)}", () => " □ ");

    /// <summary>
    /// The checkbox glyph (padded to 3 display cells) used when the checkbox
    /// is in the indeterminate state. Default is <c>" ▤ "</c> — a 1-cell
    /// U+25A4 "square with horizontal fill" framed by single-cell padding so
    /// it fills a 3-cell chip.
    /// </summary>
    public static readonly Hex1bThemeElement<string> IndeterminateBox =
        new($"{nameof(CheckboxTheme)}.{nameof(IndeterminateBox)}", () => " ▤ ");

    #endregion
}
