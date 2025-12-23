namespace Hex1b.Theming;

/// <summary>
/// Theme elements for general/global settings.
/// </summary>
public static class GlobalTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(GlobalTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(GlobalTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}
