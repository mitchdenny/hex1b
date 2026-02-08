namespace Hex1b.Theming;

/// <summary>
/// Theme elements for DragBarPanel widgets.
/// </summary>
public static class DragBarPanelTheme
{
    /// <summary>
    /// The character used for vertical handles (Left/Right edge).
    /// </summary>
    public static readonly Hex1bThemeElement<string> VerticalHandleChar = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(VerticalHandleChar)}", () => "│");
    
    /// <summary>
    /// The character used for horizontal handles (Top/Bottom edge).
    /// </summary>
    public static readonly Hex1bThemeElement<string> HorizontalHandleChar = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(HorizontalHandleChar)}", () => "─");
    
    /// <summary>
    /// Braille thumb character shown on vertical handles when hovered.
    /// </summary>
    public static readonly Hex1bThemeElement<string> VerticalThumbChar = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(VerticalThumbChar)}", () => "⣿");
    
    /// <summary>
    /// Braille thumb character shown on horizontal handles when hovered.
    /// </summary>
    public static readonly Hex1bThemeElement<string> HorizontalThumbChar = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(HorizontalThumbChar)}", () => "⠶");
    
    /// <summary>
    /// Color of the handle line in its default state.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HandleColor = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(HandleColor)}", () => Hex1bColor.Gray);
    
    /// <summary>
    /// Color of the handle line when hovered.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HandleHoverColor = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(HandleHoverColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Color of the braille thumb indicators shown on hover.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> ThumbColor = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(ThumbColor)}", () => Hex1bColor.White);
    
    /// <summary>
    /// Color of the handle when the panel is focused (for keyboard resizing).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HandleFocusedColor = 
        new($"{nameof(DragBarPanelTheme)}.{nameof(HandleFocusedColor)}", () => Hex1bColor.White);
}
