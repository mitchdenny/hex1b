namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Picker widgets.
/// </summary>
public static class PickerTheme
{
    /// <summary>
    /// Foreground color for the picker button in normal state.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(PickerTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Background color for the picker button in normal state.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(PickerTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    /// <summary>
    /// Foreground color for the picker button when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(PickerTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);
    
    /// <summary>
    /// Background color for the picker button when focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new($"{nameof(PickerTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Left bracket/delimiter for the picker display.
    /// </summary>
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new($"{nameof(PickerTheme)}.{nameof(LeftBracket)}", () => "[ ");
    
    /// <summary>
    /// Right bracket/delimiter for the picker display.
    /// </summary>
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new($"{nameof(PickerTheme)}.{nameof(RightBracket)}", () => " ▼]");
    
    /// <summary>
    /// Minimum width for the picker button (includes brackets and padding).
    /// </summary>
    public static readonly Hex1bThemeElement<int> MinimumWidth = 
        new($"{nameof(PickerTheme)}.{nameof(MinimumWidth)}", () => 10);
}
