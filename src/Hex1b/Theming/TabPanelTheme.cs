namespace Hex1b.Theming;

/// <summary>
/// Theme elements for TabPanel widgets.
/// </summary>
public static class TabPanelTheme
{
    /// <summary>
    /// Border style for the content area.
    /// </summary>
    public static readonly Hex1bThemeElement<bool> ShowContentBorder =
        new($"{nameof(TabPanelTheme)}.{nameof(ShowContentBorder)}", () => false);

    /// <summary>
    /// Foreground color for the content area.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ContentForegroundColor =
        new($"{nameof(TabPanelTheme)}.{nameof(ContentForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>
    /// Background color for the content area.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ContentBackgroundColor =
        new($"{nameof(TabPanelTheme)}.{nameof(ContentBackgroundColor)}", () => Hex1bColor.Default);
}
