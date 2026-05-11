namespace Hex1b.Theming;

/// <summary>
/// Theme elements for ToggleSwitch widgets.
/// The toggle switch is a segmented control: a single horizontal field
/// containing one option per segment, with the selected segment painted
/// as a brighter chip.
/// </summary>
/// <remarks>
/// As of the bracket-bookend cleanup, the toggle switch no longer
/// renders <c>&lt; … &gt;</c> bookends. The whole row is painted on
/// <see cref="FillBackgroundColor"/> (or
/// <see cref="FocusedFillBackgroundColor"/> when focused), with the
/// selected segment overlaying its own selected colours on top. The
/// bracket glyphs and the four BracketForeground/Background colour
/// elements that used to be configurable are gone — there is no longer
/// anything to tint.
/// </remarks>
public static class ToggleSwitchTheme
{
    /// <summary>
    /// Separator rendered between options. Defaults to <c>" │ "</c>
    /// (Unicode vertical box-drawing) for visual consistency with the
    /// other separator glyphs we ship.
    /// </summary>
    public static readonly Hex1bThemeElement<string> Separator =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(Separator)}", () => " │ ");

    /// <summary>
    /// Background colour for the entire toggle field when unfocused.
    /// Mirrors <c>TextBoxTheme.FillBackgroundColor</c> so the segmented
    /// control reads as the same family of input surface.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FillBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FillBackgroundColor)}", () => Hex1bColor.FromRgb(40, 40, 40));

    /// <summary>
    /// Background colour for the entire toggle field when focused.
    /// Slightly lighter than <see cref="FillBackgroundColor"/> to
    /// indicate the control has focus, mirroring
    /// <c>TextBoxTheme.FocusedFillBackgroundColor</c>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedFillBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedFillBackgroundColor)}", () => Hex1bColor.FromRgb(55, 55, 55));

    /// <summary>
    /// Foreground color for the selected option when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedSelectedForegroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedSelectedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background color for the selected option when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedSelectedBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedSelectedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Foreground color for the selected option when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedSelectedForegroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedSelectedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background color for the selected option when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedSelectedBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedSelectedBackgroundColor)}", () => Hex1bColor.Gray);

    /// <summary>
    /// Foreground colour for unselected options. Defaults to
    /// <see cref="Hex1bColor.Default"/> so the option text inherits the
    /// surrounding theme's text colour and reads as a normal foreground
    /// laid over the field fill.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnselectedForegroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnselectedForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Background colour for unselected options. Defaults to
    /// <see cref="Hex1bColor.Default"/>, which the renderer treats as
    /// "follow the field fill background" so unselected segments blend
    /// into the surrounding chip. Set to a concrete colour to draw
    /// unselected segments on a contrasting band.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnselectedBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnselectedBackgroundColor)}", () => Hex1bColor.Default);
}
