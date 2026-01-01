namespace Hex1b.Theming;

/// <summary>
/// Theme elements for the menu bar.
/// </summary>
public static class MenuBarTheme
{
    /// <summary>
    /// The background color of the menu bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(MenuBarTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// The foreground color of menu triggers in the bar.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(MenuBarTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// The background color of focused menu triggers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor =
        new($"{nameof(MenuBarTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// The foreground color of focused menu triggers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor =
        new($"{nameof(MenuBarTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// The foreground color of accelerator characters in menu triggers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> AcceleratorForegroundColor =
        new($"{nameof(MenuBarTheme)}.{nameof(AcceleratorForegroundColor)}", () => Hex1bColor.Yellow);

    /// <summary>
    /// The background color of accelerator characters in menu triggers.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> AcceleratorBackgroundColor =
        new($"{nameof(MenuBarTheme)}.{nameof(AcceleratorBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Whether accelerator characters should be underlined.
    /// </summary>
    public static readonly Hex1bThemeElement<bool> AcceleratorUnderline =
        new($"{nameof(MenuBarTheme)}.{nameof(AcceleratorUnderline)}", () => true);
}
