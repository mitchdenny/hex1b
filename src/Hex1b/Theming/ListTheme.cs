namespace Hex1b.Theming;

/// <summary>
/// Theme elements for List widgets.
/// </summary>
public static class ListTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(ListTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(ListTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor = 
        new($"{nameof(ListTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor = 
        new($"{nameof(ListTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor = 
        new($"{nameof(ListTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredBackgroundColor = 
        new($"{nameof(ListTheme)}.{nameof(HoveredBackgroundColor)}", () => Hex1bColor.FromRgb(180, 180, 180));
    
    public static readonly Hex1bThemeElement<string> SelectedIndicator = 
        new($"{nameof(ListTheme)}.{nameof(SelectedIndicator)}", () => "> ");
    
    public static readonly Hex1bThemeElement<string> UnselectedIndicator = 
        new($"{nameof(ListTheme)}.{nameof(UnselectedIndicator)}", () => "  ");
}
