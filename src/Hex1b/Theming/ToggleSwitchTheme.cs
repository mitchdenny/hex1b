namespace Hex1b.Theming;

/// <summary>
/// Theme elements for ToggleSwitch widgets.
/// </summary>
/// <remarks>
/// The toggle switch renders as a horizontal strip of per-option chips.
/// Each option occupies <c>1 + label_length + 1</c> cells (a single
/// padding cell on each side of the label) and is painted in either
/// the unselected colours or — when it is the active option — the
/// selected colours. There is no separator glyph and no outer "field"
/// background; adjacent option chips simply tile against each other.
/// <para>
/// The selected option has two colour pairs: focused (used when the
/// toggle itself has focus) and unfocused (used when the toggle is
/// inactive). Keeping both lets the selected segment pop more brightly
/// when the toggle is the active focus target without needing an
/// outer field tint to advertise focus.
/// </para>
/// </remarks>
public static class ToggleSwitchTheme
{
    /// <summary>
    /// Foreground color for the selected option when the toggle has focus.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedSelectedForegroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedSelectedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background color for the selected option when the toggle has focus.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedSelectedBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedSelectedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Foreground color for the selected option when the toggle does not have focus.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedSelectedForegroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedSelectedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background color for the selected option when the toggle does not have focus.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedSelectedBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedSelectedBackgroundColor)}", () => Hex1bColor.Gray);

    /// <summary>
    /// Foreground colour for unselected option chips. Defaults to
    /// <see cref="Hex1bColor.Default"/> so unselected labels inherit
    /// the surrounding theme's text colour.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnselectedForegroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnselectedForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Background colour for unselected option chips. Defaults to
    /// <c>rgb(40, 40, 40)</c>, which paints the unselected segments as
    /// a recessed dark chip body — the same tone as
    /// <c>TextBoxTheme.FillBackgroundColor</c> so the toggle reads as
    /// part of the same family of input surfaces. Set to
    /// <see cref="Hex1bColor.Default"/> to disable the chip background
    /// entirely and let unselected segments inherit the surrounding
    /// background.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnselectedBackgroundColor =
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnselectedBackgroundColor)}", () => Hex1bColor.FromRgb(40, 40, 40));
}
