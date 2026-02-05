namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Window widgets.
/// </summary>
public static class WindowTheme
{
    // Title bar colors - monochrome theme
    public static readonly Hex1bThemeElement<Hex1bColor> TitleBarForeground = 
        new($"{nameof(WindowTheme)}.{nameof(TitleBarForeground)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> TitleBarBackground = 
        new($"{nameof(WindowTheme)}.{nameof(TitleBarBackground)}", () => Hex1bColor.Gray);
    
    public static readonly Hex1bThemeElement<Hex1bColor> TitleBarActiveForeground = 
        new($"{nameof(WindowTheme)}.{nameof(TitleBarActiveForeground)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> TitleBarActiveBackground = 
        new($"{nameof(WindowTheme)}.{nameof(TitleBarActiveBackground)}", () => Hex1bColor.White);
    
    // Border colors - monochrome
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor = 
        new($"{nameof(WindowTheme)}.{nameof(BorderColor)}", () => Hex1bColor.Gray);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BorderActiveColor = 
        new($"{nameof(WindowTheme)}.{nameof(BorderActiveColor)}", () => Hex1bColor.White);
    
    // Content area - monochrome (use terminal default)
    public static readonly Hex1bThemeElement<Hex1bColor> ContentBackground = 
        new($"{nameof(WindowTheme)}.{nameof(ContentBackground)}", () => Hex1bColor.Black);
    
    // Close button
    public static readonly Hex1bThemeElement<string> CloseButtonGlyph = 
        new($"{nameof(WindowTheme)}.{nameof(CloseButtonGlyph)}", () => "×");
    
    public static readonly Hex1bThemeElement<Hex1bColor> CloseButtonForeground = 
        new($"{nameof(WindowTheme)}.{nameof(CloseButtonForeground)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CloseButtonHoverBackground = 
        new($"{nameof(WindowTheme)}.{nameof(CloseButtonHoverBackground)}", () => Hex1bColor.FromRgb(200, 50, 50));
    
    // Minimize button
    public static readonly Hex1bThemeElement<string> MinimizeButtonGlyph = 
        new($"{nameof(WindowTheme)}.{nameof(MinimizeButtonGlyph)}", () => "−");
    
    public static readonly Hex1bThemeElement<Hex1bColor> MinimizeButtonForeground = 
        new($"{nameof(WindowTheme)}.{nameof(MinimizeButtonForeground)}", () => Hex1bColor.White);
    
    // Maximize button
    public static readonly Hex1bThemeElement<string> MaximizeButtonGlyph = 
        new($"{nameof(WindowTheme)}.{nameof(MaximizeButtonGlyph)}", () => "□");
    
    public static readonly Hex1bThemeElement<string> RestoreButtonGlyph = 
        new($"{nameof(WindowTheme)}.{nameof(RestoreButtonGlyph)}", () => "◱");
    
    public static readonly Hex1bThemeElement<Hex1bColor> MaximizeButtonForeground = 
        new($"{nameof(WindowTheme)}.{nameof(MaximizeButtonForeground)}", () => Hex1bColor.White);
    
    // Border characters
    public static readonly Hex1bThemeElement<string> TopLeftCorner = 
        new($"{nameof(WindowTheme)}.{nameof(TopLeftCorner)}", () => "┌");
    
    public static readonly Hex1bThemeElement<string> TopRightCorner = 
        new($"{nameof(WindowTheme)}.{nameof(TopRightCorner)}", () => "┐");
    
    public static readonly Hex1bThemeElement<string> BottomLeftCorner = 
        new($"{nameof(WindowTheme)}.{nameof(BottomLeftCorner)}", () => "└");
    
    public static readonly Hex1bThemeElement<string> BottomRightCorner = 
        new($"{nameof(WindowTheme)}.{nameof(BottomRightCorner)}", () => "┘");
    
    /// <summary>
    /// Glyph shown in bottom-right corner of resizable windows.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeGrip = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeGrip)}", () => "◢");
    
    public static readonly Hex1bThemeElement<string> HorizontalLine = 
        new($"{nameof(WindowTheme)}.{nameof(HorizontalLine)}", () => "─");
    
    public static readonly Hex1bThemeElement<string> VerticalLine = 
        new($"{nameof(WindowTheme)}.{nameof(VerticalLine)}", () => "│");

    // Resize thumb indicators (shown on hover)
    /// <summary>
    /// Thumb shown on horizontal edges when hovered for resize.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeThumbHorizontal = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbHorizontal)}", () => "⠶");

    /// <summary>
    /// Thumb shown on vertical edges when hovered for resize.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeThumbVertical = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbVertical)}", () => "⣿");

    /// <summary>
    /// Thumb shown on top-left corner when hovered for resize.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeThumbTopLeft = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbTopLeft)}", () => "⣶");

    /// <summary>
    /// Thumb shown on top-right corner when hovered for resize.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeThumbTopRight = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbTopRight)}", () => "⣶");

    /// <summary>
    /// Thumb shown on bottom-left corner when hovered for resize.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeThumbBottomLeft = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbBottomLeft)}", () => "⠿");

    /// <summary>
    /// Thumb shown on bottom-right corner when hovered for resize.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ResizeThumbBottomRight = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbBottomRight)}", () => "⠿");

    /// <summary>
    /// Color of resize thumb indicators.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ResizeThumbColor = 
        new($"{nameof(WindowTheme)}.{nameof(ResizeThumbColor)}", () => Hex1bColor.White);
}
