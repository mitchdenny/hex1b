namespace Hex1b.Theming;

/// <summary>
/// Theme elements for NotificationCard widgets.
/// </summary>
/// <remarks>
/// Default colors use a dark gray background with bright title and muted body text.
/// Progress bar uses braille characters for a thin, subtle appearance.
/// </remarks>
public static class NotificationCardTheme
{
    /// <summary>
    /// Background color for notification cards.
    /// Default is dark gray.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(45, 45, 45));
    
    /// <summary>
    /// Foreground color for the notification title (bright).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TitleColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(TitleColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Foreground color for the notification body text (muted).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BodyColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(BodyColor)}", () => Hex1bColor.FromRgb(160, 160, 160));
    
    /// <summary>
    /// Foreground color for action buttons.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ActionColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ActionColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Background color when the notification card is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.FromRgb(60, 60, 80));
    
    /// <summary>
    /// Color for the progress bar (timeout indicator). Bright color.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ProgressBarColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ProgressBarColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Color for the dismiss button text.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DismissButtonColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(DismissButtonColor)}", () => Hex1bColor.FromRgb(120, 120, 120));
    
    /// <summary>
    /// Character for a fully filled progress bar cell.
    /// Default is '⣀' (U+28C0, braille dots 7,8 - two bottom dots).
    /// </summary>
    public static readonly Hex1bThemeElement<char> ProgressFilledCharacter = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ProgressFilledCharacter)}", () => '⣀');
    
    /// <summary>
    /// Character for a left-half filled progress bar cell.
    /// Default is '⡀' (U+2840, braille dot 7 - left bottom dot).
    /// Used at the trailing edge when progress is decreasing (countdown).
    /// </summary>
    public static readonly Hex1bThemeElement<char> ProgressLeftHalfCharacter = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ProgressLeftHalfCharacter)}", () => '⡀');
    
    /// <summary>
    /// Character for a right-half filled progress bar cell.
    /// Default is '⢀' (U+2880, braille dot 8 - right bottom dot).
    /// Used at the trailing edge when progress is increasing.
    /// </summary>
    public static readonly Hex1bThemeElement<char> ProgressRightHalfCharacter = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ProgressRightHalfCharacter)}", () => '⢀');
}
