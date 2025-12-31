namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Button widgets.
/// </summary>
public static class ButtonTheme
{
    public static readonly Hex1bThemeElement<int> MinimumWidth = 
        new($"{nameof(ButtonTheme)}.{nameof(MinimumWidth)}", () => 10);
    
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredForegroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(HoveredForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoveredBackgroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(HoveredBackgroundColor)}", () => Hex1bColor.FromRgb(180, 180, 180));
    
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new($"{nameof(ButtonTheme)}.{nameof(LeftBracket)}", () => "[ ");
    
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new($"{nameof(ButtonTheme)}.{nameof(RightBracket)}", () => " ]");
}
