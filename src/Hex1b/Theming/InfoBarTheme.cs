namespace Hex1b.Theming;

/// <summary>
/// Theme elements for InfoBar widgets.
/// By default, InfoBar uses inverted colors (swaps foreground/background).
/// </summary>
public static class InfoBarTheme
{
    /// <summary>
    /// Foreground color for the info bar. When InvertColors is true (default),
    /// this becomes the background and the background becomes the foreground.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(InfoBarTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for the info bar. When InvertColors is true (default),
    /// this becomes the foreground and the foreground becomes the background.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(InfoBarTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
