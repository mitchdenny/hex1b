namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Button widgets.
/// </summary>
public static class ButtonTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(ButtonTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Resting chip background. Defaults to a slightly brighter grey than the
    /// input-field family (TextBox / ToggleSwitch unselected use rgb(40,40,40))
    /// so buttons read as sitting visually above input surfaces. Set to
    /// <see cref="Hex1bColor.Default"/> to disable the chip background.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(ButtonTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(60, 60, 60));

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor =
        new($"{nameof(ButtonTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor =
        new($"{nameof(ButtonTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);

    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor =
        new($"{nameof(ButtonTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.Black);

    public static readonly Hex1bThemeElement<Hex1bColor> HoveredBackgroundColor =
        new($"{nameof(ButtonTheme)}.{nameof(HoveredBackgroundColor)}", () => Hex1bColor.FromRgb(180, 180, 180));
}
