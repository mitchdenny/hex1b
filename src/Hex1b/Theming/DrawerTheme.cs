namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Drawer widgets.
/// </summary>
public static class DrawerTheme
{
    /// <summary>
    /// Background color of the header row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderBackground = 
        new($"{nameof(DrawerTheme)}.{nameof(HeaderBackground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Text/foreground color of the header row.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderForeground = 
        new($"{nameof(DrawerTheme)}.{nameof(HeaderForeground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color of the expanded content area.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ContentBackground = 
        new($"{nameof(DrawerTheme)}.{nameof(ContentBackground)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Color of the drawer border.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor = 
        new($"{nameof(DrawerTheme)}.{nameof(BorderColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// Indicator shown when the drawer is expanded.
    /// </summary>
    public static readonly Hex1bThemeElement<string> ExpandedIndicator = 
        new($"{nameof(DrawerTheme)}.{nameof(ExpandedIndicator)}", () => "▼");
    
    /// <summary>
    /// Indicator for collapsed state when positioned on the left.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CollapsedIndicatorLeft = 
        new($"{nameof(DrawerTheme)}.{nameof(CollapsedIndicatorLeft)}", () => "▶");
    
    /// <summary>
    /// Indicator for collapsed state when positioned on the right.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CollapsedIndicatorRight = 
        new($"{nameof(DrawerTheme)}.{nameof(CollapsedIndicatorRight)}", () => "◀");
    
    /// <summary>
    /// Indicator for collapsed state when positioned at the top.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CollapsedIndicatorTop = 
        new($"{nameof(DrawerTheme)}.{nameof(CollapsedIndicatorTop)}", () => "▼");
    
    /// <summary>
    /// Indicator for collapsed state when positioned at the bottom.
    /// </summary>
    public static readonly Hex1bThemeElement<string> CollapsedIndicatorBottom = 
        new($"{nameof(DrawerTheme)}.{nameof(CollapsedIndicatorBottom)}", () => "▲");
    
    /// <summary>
    /// Indicator color when the toggle is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedIndicatorColor = 
        new($"{nameof(DrawerTheme)}.{nameof(FocusedIndicatorColor)}", () => Hex1bColor.Cyan);
    
    /// <summary>
    /// Background color when the toggle is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackground = 
        new($"{nameof(DrawerTheme)}.{nameof(FocusedBackground)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Foreground color when the toggle is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForeground = 
        new($"{nameof(DrawerTheme)}.{nameof(FocusedForeground)}", () => Hex1bColor.Black);
}
