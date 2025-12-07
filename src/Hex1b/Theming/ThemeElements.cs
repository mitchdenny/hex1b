namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Button widgets.
/// </summary>
public static class ButtonTheme
{
    public static readonly Hex1bThemeElement<int> MinimumWidth = 
        new("Button.MinimumWidth", () => 10);
    
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new("Button.ForegroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new("Button.BackgroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new("Button.FocusedForegroundColor", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedBackgroundColor = 
        new("Button.FocusedBackgroundColor", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new("Button.LeftBracket", () => "[ ");
    
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new("Button.RightBracket", () => " ]");
}

/// <summary>
/// Theme elements for TextBox widgets.
/// </summary>
public static class TextBoxTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new("TextBox.ForegroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new("TextBox.BackgroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> FocusedForegroundColor = 
        new("TextBox.FocusedForegroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorForegroundColor = 
        new("TextBox.CursorForegroundColor", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> CursorBackgroundColor = 
        new("TextBox.CursorBackgroundColor", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionForegroundColor = 
        new("TextBox.SelectionForegroundColor", () => Hex1bColor.Black);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectionBackgroundColor = 
        new("TextBox.SelectionBackgroundColor", () => Hex1bColor.Cyan);
    
    public static readonly Hex1bThemeElement<string> LeftBracket = 
        new("TextBox.LeftBracket", () => "[");
    
    public static readonly Hex1bThemeElement<string> RightBracket = 
        new("TextBox.RightBracket", () => "]");
}

/// <summary>
/// Theme elements for List widgets.
/// </summary>
public static class ListTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new("List.ForegroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new("List.BackgroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor = 
        new("List.SelectedForegroundColor", () => Hex1bColor.White);
    
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor = 
        new("List.SelectedBackgroundColor", () => Hex1bColor.Blue);
    
    public static readonly Hex1bThemeElement<string> SelectedIndicator = 
        new("List.SelectedIndicator", () => "> ");
    
    public static readonly Hex1bThemeElement<string> UnselectedIndicator = 
        new("List.UnselectedIndicator", () => "  ");
}

/// <summary>
/// Theme elements for Splitter widgets.
/// </summary>
public static class SplitterTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> DividerColor = 
        new("Splitter.DividerColor", () => Hex1bColor.Gray);
    
    public static readonly Hex1bThemeElement<string> DividerCharacter = 
        new("Splitter.DividerCharacter", () => "│");
}

/// <summary>
/// Theme elements for general/global settings.
/// </summary>
public static class GlobalTheme
{
    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor = 
        new("Global.ForegroundColor", () => Hex1bColor.Default);
    
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor = 
        new("Global.BackgroundColor", () => Hex1bColor.Default);
}
