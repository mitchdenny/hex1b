namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Splitter widgets.
/// </summary>
public static class SplitterTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> DividerColor = 
        new($"{nameof(SplitterTheme)}.{nameof(DividerColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// The character used for vertical dividers (horizontal orientation splitter).
    /// </summary>
    public static readonly Hex1bThemeElement<string> DividerCharacter = 
        new($"{nameof(SplitterTheme)}.{nameof(DividerCharacter)}", () => "│");
    
    /// <summary>
    /// The character used for horizontal dividers (vertical orientation splitter).
    /// </summary>
    public static readonly Hex1bThemeElement<string> HorizontalDividerCharacter = 
        new($"{nameof(SplitterTheme)}.{nameof(HorizontalDividerCharacter)}", () => "─");
    
    /// <summary>
    /// The left arrow character shown on horizontal splitters to indicate they can be moved.
    /// </summary>
    public static readonly Hex1bThemeElement<string> LeftArrowCharacter = 
        new($"{nameof(SplitterTheme)}.{nameof(LeftArrowCharacter)}", () => "←");
    
    /// <summary>
    /// The color of the left arrow on horizontal splitters. Defaults to divider color if not set.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LeftArrowColor = 
        new($"{nameof(SplitterTheme)}.{nameof(LeftArrowColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// The right arrow character shown on horizontal splitters to indicate they can be moved.
    /// </summary>
    public static readonly Hex1bThemeElement<string> RightArrowCharacter = 
        new($"{nameof(SplitterTheme)}.{nameof(RightArrowCharacter)}", () => "→");
    
    /// <summary>
    /// The color of the right arrow on horizontal splitters. Defaults to divider color if not set.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> RightArrowColor = 
        new($"{nameof(SplitterTheme)}.{nameof(RightArrowColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// The up arrow character shown on vertical splitters to indicate they can be moved.
    /// </summary>
    public static readonly Hex1bThemeElement<string> UpArrowCharacter = 
        new($"{nameof(SplitterTheme)}.{nameof(UpArrowCharacter)}", () => "↑");
    
    /// <summary>
    /// The color of the up arrow on vertical splitters. Defaults to divider color if not set.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UpArrowColor = 
        new($"{nameof(SplitterTheme)}.{nameof(UpArrowColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// The down arrow character shown on vertical splitters to indicate they can be moved.
    /// </summary>
    public static readonly Hex1bThemeElement<string> DownArrowCharacter = 
        new($"{nameof(SplitterTheme)}.{nameof(DownArrowCharacter)}", () => "↓");
    
    /// <summary>
    /// The color of the down arrow on vertical splitters. Defaults to divider color if not set.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DownArrowColor = 
        new($"{nameof(SplitterTheme)}.{nameof(DownArrowColor)}", () => Hex1bColor.Default);
}
