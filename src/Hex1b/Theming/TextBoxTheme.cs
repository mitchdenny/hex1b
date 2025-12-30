namespace Hex1b.Theming;

/// <summary>
/// Theme elements for TextBox widgets.
/// </summary>
public static class TextBoxTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(CursorForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(CursorBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(SelectionForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(SelectionBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoverCursorForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(HoverCursorForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoverCursorBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(HoverCursorBackgroundColor)}", () => Hex1bColor.DarkGray);
    
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new($"{nameof(TextBoxTheme)}.{nameof(LeftBracket)}", () => "[");
    
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new($"{nameof(TextBoxTheme)}.{nameof(RightBracket)}", () => "]");
}
