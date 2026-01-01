namespace Hex1b.Theming;

/// <summary>
/// Theme elements for menu items.
/// </summary>
public static class MenuItemTheme
{
    /// <summary>
    /// The foreground color of menu items.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// The background color of menu items.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// The foreground color of focused menu items.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// The background color of focused menu items.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// The foreground color of disabled menu items.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DisabledForegroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(DisabledForegroundColor)}", () => Hex1bColor.Gray);

    /// <summary>
    /// The foreground color of accelerator characters.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> AcceleratorForegroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(AcceleratorForegroundColor)}", () => Hex1bColor.Yellow);

    /// <summary>
    /// The background color of accelerator characters.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> AcceleratorBackgroundColor =
        new($"{nameof(MenuItemTheme)}.{nameof(AcceleratorBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Whether accelerator characters should be underlined.
    /// </summary>
    public static readonly Hex1bThemeElement<bool> AcceleratorUnderline =
        new($"{nameof(MenuItemTheme)}.{nameof(AcceleratorUnderline)}", () => true);

    /// <summary>
    /// The indicator string for submenus (appears at end of submenu items).
    /// </summary>
    public static readonly Hex1bThemeElement<string> SubmenuIndicator =
        new($"{nameof(MenuItemTheme)}.{nameof(SubmenuIndicator)}", () => " ►");
}
