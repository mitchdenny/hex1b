namespace Hex1b.Theming;

/// <summary>
/// Theme elements for ToggleSwitch widgets.
/// The toggle switch displays multiple options horizontally, with the selected option highlighted.
/// </summary>
public static class ToggleSwitchTheme
{
    /// <summary>
    /// Left bracket character(s) for the toggle switch.
    /// </summary>
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(LeftBracket)}", () => "< ");
    
    /// <summary>
    /// Right bracket character(s) for the toggle switch.
    /// </summary>
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(RightBracket)}", () => " >");
    
    /// <summary>
    /// Separator between options.
    /// </summary>
    public static readonly Hex1bThemeElement<string> Separator = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(Separator)}", () => " | ");
    
    /// <summary>
    /// Foreground color for the selected option when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedSelectedForegroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedSelectedForegroundColor)}", () => Hex1bColor.Black);
    
    /// <summary>
    /// Background color for the selected option when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedSelectedBackgroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedSelectedBackgroundColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Foreground color for the selected option when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedSelectedForegroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedSelectedForegroundColor)}", () => Hex1bColor.Black);
    
    /// <summary>
    /// Background color for the selected option when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedSelectedBackgroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedSelectedBackgroundColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// Foreground color for unselected options.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnselectedForegroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnselectedForegroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for unselected options.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnselectedBackgroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnselectedBackgroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for brackets when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBracketForegroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedBracketForegroundColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Background color for brackets when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBracketBackgroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(FocusedBracketBackgroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for brackets when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedBracketForegroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedBracketForegroundColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// Background color for brackets when unfocused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> UnfocusedBracketBackgroundColor = 
        new($"{nameof(ToggleSwitchTheme)}.{nameof(UnfocusedBracketBackgroundColor)}", () => Hex1bColor.Default);
}
