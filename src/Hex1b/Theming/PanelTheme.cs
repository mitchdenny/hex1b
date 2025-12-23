namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Panel widgets.
/// </summary>
public static class PanelTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(PanelTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(PanelTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
