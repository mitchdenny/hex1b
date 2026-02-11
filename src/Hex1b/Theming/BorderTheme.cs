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
    
    #region Per-Side Line Characters
    
    /// <summary>
    /// Line character for the top border. Falls back to <see cref="HorizontalLine"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<string> TopLine = 
        new($"{nameof(BorderTheme)}.{nameof(TopLine)}", () => "─", HorizontalLine);
    
    /// <summary>
    /// Line character for the bottom border. Falls back to <see cref="HorizontalLine"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<string> BottomLine = 
        new($"{nameof(BorderTheme)}.{nameof(BottomLine)}", () => "─", HorizontalLine);
    
    /// <summary>
    /// Line character for the left border. Falls back to <see cref="VerticalLine"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<string> LeftLine = 
        new($"{nameof(BorderTheme)}.{nameof(LeftLine)}", () => "│", VerticalLine);
    
    /// <summary>
    /// Line character for the right border. Falls back to <see cref="VerticalLine"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<string> RightLine = 
        new($"{nameof(BorderTheme)}.{nameof(RightLine)}", () => "│", VerticalLine);
    
    #endregion
    
    #region Per-Side Border Colors
    
    /// <summary>
    /// Color for horizontal borders (top and bottom). Falls back to <see cref="BorderColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HorizontalBorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(HorizontalBorderColor)}", () => Hex1bColor.Gray, BorderColor);
    
    /// <summary>
    /// Color for vertical borders (left and right). Falls back to <see cref="BorderColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> VerticalBorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(VerticalBorderColor)}", () => Hex1bColor.Gray, BorderColor);
    
    /// <summary>
    /// Color for the top border. Falls back to <see cref="HorizontalBorderColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TopBorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(TopBorderColor)}", () => Hex1bColor.Gray, HorizontalBorderColor);
    
    /// <summary>
    /// Color for the bottom border. Falls back to <see cref="HorizontalBorderColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BottomBorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(BottomBorderColor)}", () => Hex1bColor.Gray, HorizontalBorderColor);
    
    /// <summary>
    /// Color for the left border. Falls back to <see cref="VerticalBorderColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LeftBorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(LeftBorderColor)}", () => Hex1bColor.Gray, VerticalBorderColor);
    
    /// <summary>
    /// Color for the right border. Falls back to <see cref="VerticalBorderColor"/>.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> RightBorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(RightBorderColor)}", () => Hex1bColor.Gray, VerticalBorderColor);
    
    #endregion
}
