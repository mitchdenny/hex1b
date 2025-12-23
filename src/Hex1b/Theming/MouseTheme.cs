namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Mouse cursor styling.
/// The mouse cursor is rendered as an overlay on the character under the mouse position.
/// </summary>
public static class MouseTheme
{
    /// <summary>
    /// Foreground color for the character under the mouse cursor.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CursorForegroundColor = 
        new($"{nameof(MouseTheme)}.{nameof(CursorForegroundColor)}", () => Hex1bColor.Black);
    
    /// <summary>
    /// Background color for the character under the mouse cursor.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CursorBackgroundColor = 
        new($"{nameof(MouseTheme)}.{nameof(CursorBackgroundColor)}", () => Hex1bColor.Yellow);
    
    /// <summary>
    /// Whether to show the mouse cursor. Set to false to disable mouse cursor rendering.
    /// </summary>
    public static readonly Hex1bThemeElement<bool> ShowCursor = 
        new($"{nameof(MouseTheme)}.{nameof(ShowCursor)}", () => true);
}
