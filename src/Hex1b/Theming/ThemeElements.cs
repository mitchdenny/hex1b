namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Button widgets.
/// </summary>
public static class ButtonTheme
{
    public static readonly Hex1bThemeElement<int> MinimumWidth = 
        new($"{nameof(ButtonTheme)}.{nameof(MinimumWidth)}", () => 10);
    
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new($"{nameof(ButtonTheme)}.{nameof(FocusedBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new($"{nameof(ButtonTheme)}.{nameof(LeftBracket)}", () => "[ ");
    
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new($"{nameof(ButtonTheme)}.{nameof(RightBracket)}", () => " ]");
}

/// <summary>
/// Theme elements for TextBox widgets.
/// </summary>
public static class TextBoxTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(FocusedForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(CursorForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(CursorBackgroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(SelectionForegroundColor)}", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(SelectionBackgroundColor)}", () => Hex1bColor.Cyan);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoverCursorForegroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(HoverCursorForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> HoverCursorBackgroundColor = 
        new($"{nameof(TextBoxTheme)}.{nameof(HoverCursorBackgroundColor)}", () => Hex1bColor.DarkGray);
    
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new($"{nameof(TextBoxTheme)}.{nameof(LeftBracket)}", () => "[");
    
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new($"{nameof(TextBoxTheme)}.{nameof(RightBracket)}", () => "]");
}

/// <summary>
/// Theme elements for List widgets.
/// </summary>
public static class ListTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(ListTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(ListTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor = 
        new($"{nameof(ListTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor = 
        new($"{nameof(ListTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.Blue);
    
    public static readonly Hex1bThemeElement<string> SelectedIndicator = 
        new($"{nameof(ListTheme)}.{nameof(SelectedIndicator)}", () => "> ");
    
    public static readonly Hex1bThemeElement<string> UnselectedIndicator = 
        new($"{nameof(ListTheme)}.{nameof(UnselectedIndicator)}", () => "  ");
}

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
}

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

/// <summary>
/// Theme elements for Border widgets.
/// </summary>
public static class BorderTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor = 
        new($"{nameof(BorderTheme)}.{nameof(BorderColor)}", () => Hex1bColor.Gray);
    
    public static readonly Hex1bThemeElement<Hex1bColor> TitleColor = 
        new($"{nameof(BorderTheme)}.{nameof(TitleColor)}", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<string> TopLeftCorner = 
        new($"{nameof(BorderTheme)}.{nameof(TopLeftCorner)}", () => "┌");
    
    public static readonly Hex1bThemeElement<string> TopRightCorner = 
        new($"{nameof(BorderTheme)}.{nameof(TopRightCorner)}", () => "┐");
    
    public static readonly Hex1bThemeElement<string> BottomLeftCorner = 
        new($"{nameof(BorderTheme)}.{nameof(BottomLeftCorner)}", () => "└");
    
    public static readonly Hex1bThemeElement<string> BottomRightCorner = 
        new($"{nameof(BorderTheme)}.{nameof(BottomRightCorner)}", () => "┘");
    
    public static readonly Hex1bThemeElement<string> HorizontalLine = 
        new($"{nameof(BorderTheme)}.{nameof(HorizontalLine)}", () => "─");
    
    public static readonly Hex1bThemeElement<string> VerticalLine = 
        new($"{nameof(BorderTheme)}.{nameof(VerticalLine)}", () => "│");
}

/// <summary>
/// Theme elements for Panel widgets.
/// </summary>
public static class PanelTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new($"{nameof(PanelTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new($"{nameof(PanelTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.Default);
}

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
