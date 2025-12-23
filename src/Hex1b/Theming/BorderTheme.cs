namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Border widgets.
/// </summary>
public static class BorderTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(BorderColor)}", () => Hex1bColor.Gray);
    
    public static readonly Hex1bThemeElement<Hex1bColor> TitleColor = 
        new($"{nameof(BorderTheme)}.{nameof(TitleColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<string> TopLeftCorner = 
        new($"{nameof(BorderTheme)}.{nameof(TopLeftCorner)}", () => "┌");
    
    public static readonly Hex1bThemeElement<string> TopRightCorner = 
        new($"{nameof(BorderTheme)}.{nameof(TopRightCorner)}", () => "┐");
    
    public static readonly Hex1bThemeElement<string> BottomLeftCorner = 
        new($"{nameof(BorderTheme)}.{nameof(BottomLeftCorner)}", () => "└");
    
    public static readonly Hex1bThemeElement<string> BottomRightCorner = 
        new($"{nameof(BorderTheme)}.{nameof(BottomRightCorner)}", () => "┘");
    
    public static readonly Hex1bThemeElement<string> HorizontalLine = 
        new($"{nameof(BorderTheme)}.{nameof(HorizontalLine)}", () => "─");
    
    public static readonly Hex1bThemeElement<string> VerticalLine = 
        new($"{nameof(BorderTheme)}.{nameof(VerticalLine)}", () => "│");
}
