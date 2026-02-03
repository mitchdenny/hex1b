namespace Hex1b.Theming;

/// <summary>
/// Theme elements for IconWidget.
/// </summary>
public static class IconTheme
{
    /// <summary>
    /// Foreground color for icons.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(IconTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for icons.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(IconTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
