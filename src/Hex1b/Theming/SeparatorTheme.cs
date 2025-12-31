namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Separator widgets.
/// </summary>
public static class SeparatorTheme
{
    /// <summary>
    /// The color of the separator line.
    /// If default, falls back to GlobalTheme.ForegroundColor.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> Color = 
        new($"{nameof(SeparatorTheme)}.{nameof(Color)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// The character to use for horizontal separators.
    /// </summary>
    public static readonly Hex1bThemeElement<string> HorizontalChar = 
        new($"{nameof(SeparatorTheme)}.{nameof(HorizontalChar)}", () => "─");
    
    /// <summary>
    /// The character to use for vertical separators.
    /// </summary>
    public static readonly Hex1bThemeElement<string> VerticalChar = 
        new($"{nameof(SeparatorTheme)}.{nameof(VerticalChar)}", () => "│");
}
