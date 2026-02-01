namespace Hex1b.Theming;

/// <summary>
/// Theme elements for NotificationCard widgets.
/// </summary>
/// <remarks>
/// By default, notification cards use inverted colors (foreground becomes background and vice versa)
/// to make them stand out from the underlying content.
/// </remarks>
public static class NotificationCardTheme
{
    /// <summary>
    /// Foreground color for notification card text.
    /// Default is Black (inverted from typical white-on-black terminal).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Black);
    
    /// <summary>
    /// Background color for notification cards.
    /// Default is White (inverted from typical white-on-black terminal).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Foreground color when the notification card is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Background color when the notification card is focused.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.Blue);
    
    /// <summary>
    /// Color for the progress bar (timeout indicator).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ProgressBarColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(ProgressBarColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// Color for the dismiss button text.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DismissButtonColor = 
        new($"{nameof(NotificationCardTheme)}.{nameof(DismissButtonColor)}", () => Hex1bColor.DarkGray);
}
