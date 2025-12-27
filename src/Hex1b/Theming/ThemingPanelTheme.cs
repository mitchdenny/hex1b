namespace Hex1b.Theming;

/// <summary>
/// Theme elements for ThemingPanel widgets.
/// </summary>
public static class ThemingPanelTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(ThemingPanelTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(ThemingPanelTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
