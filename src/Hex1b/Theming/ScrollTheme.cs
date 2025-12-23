namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Scroll widgets.
/// </summary>
public static class ScrollTheme
{
    /// <summary>
    /// The track character for vertical scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> VerticalTrackCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(VerticalTrackCharacter)}", () => "░");
    
    /// <summary>
    /// The thumb character for vertical scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> VerticalThumbCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(VerticalThumbCharacter)}", () => "█");
    
    /// <summary>
    /// The track character for horizontal scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> HorizontalTrackCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(HorizontalTrackCharacter)}", () => "░");
    
    /// <summary>
    /// The thumb character for horizontal scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> HorizontalThumbCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(HorizontalThumbCharacter)}", () => "█");
    
    /// <summary>
    /// The color of the scrollbar track.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TrackColor = 
        new($"{nameof(ScrollTheme)}.{nameof(TrackColor)}", () => Hex1bColor.DarkGray);
    
    /// <summary>
    /// The color of the scrollbar thumb.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ThumbColor = 
        new($"{nameof(ScrollTheme)}.{nameof(ThumbColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// The color of the scrollbar thumb when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedThumbColor = 
        new($"{nameof(ScrollTheme)}.{nameof(FocusedThumbColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// The up arrow character for vertical scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> UpArrowCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(UpArrowCharacter)}", () => "▲");
    
    /// <summary>
    /// The down arrow character for vertical scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> DownArrowCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(DownArrowCharacter)}", () => "▼");
    
    /// <summary>
    /// The left arrow character for horizontal scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> LeftArrowCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(LeftArrowCharacter)}", () => "◀");
    
    /// <summary>
    /// The right arrow character for horizontal scrollbars.
    /// </summary>
    public static readonly Hex1bThemeElement<string> RightArrowCharacter = 
        new($"{nameof(ScrollTheme)}.{nameof(RightArrowCharacter)}", () => "▶");
}
