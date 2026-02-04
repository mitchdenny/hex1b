namespace Hex1b.Theming;

/// <summary>
/// Theme elements for TabBar widgets.
/// </summary>
public static class TabBarTheme
{
    /// <summary>
    /// Foreground color for unselected tabs.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Background color for unselected tabs.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Foreground color for the selected tab.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>
    /// Background color for the selected tab.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Foreground color for disabled tabs.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DisabledForegroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(DisabledForegroundColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Foreground color for arrow buttons (enabled).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ArrowForegroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(ArrowForegroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Foreground color for arrow buttons (disabled).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ArrowDisabledColor =
        new($"{nameof(TabBarTheme)}.{nameof(ArrowDisabledColor)}", () => Hex1bColor.DarkGray);

    /// <summary>
    /// Foreground color for the dropdown button.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DropdownForegroundColor =
        new($"{nameof(TabBarTheme)}.{nameof(DropdownForegroundColor)}", () => Hex1bColor.White);

    /// <summary>
    /// Separator character between tabs (optional).
    /// </summary>
    public static readonly Hex1bThemeElement<string> TabSeparator =
        new($"{nameof(TabBarTheme)}.{nameof(TabSeparator)}", () => "");
}
