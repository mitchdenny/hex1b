namespace Hex1b.Theming;

/// <summary>
/// Provides pre-built themes for Hex1b applications.
/// </summary>
public static class Hex1bThemes
{
    /// <summary>
    /// The default theme with minimal styling.
    /// </summary>
    public static Hex1bTheme Default { get; } = CreateDefaultTheme();

    /// <summary>
    /// A dark theme with blue accents.
    /// </summary>
    public static Hex1bTheme Ocean { get; } = CreateOceanTheme();

    /// <summary>
    /// A high-contrast theme.
    /// </summary>
    public static Hex1bTheme HighContrast { get; } = CreateHighContrastTheme();

    /// <summary>
    /// A warm theme with orange/red accents.
    /// </summary>
    public static Hex1bTheme Sunset { get; } = CreateSunsetTheme();

    private static Hex1bTheme CreateDefaultTheme()
    {
        return new Hex1bTheme("Default").Lock();
        // Uses all default values from theme elements
    }

    private static Hex1bTheme CreateOceanTheme()
    {
        return new Hex1bTheme("Ocean")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 100, 180))
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(100, 200, 255))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(0, 80, 140))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.White)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 100, 180))
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(0, 120, 200))
            // ToggleSwitch
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.White)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.FromRgb(0, 100, 180))
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.FromRgb(100, 200, 255))
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(0, 50, 90))
            .Set(ToggleSwitchTheme.FocusedBracketForegroundColor, Hex1bColor.FromRgb(100, 200, 255))
            .Lock();
    }

    private static Hex1bTheme CreateHighContrastTheme()
    {
        return new Hex1bTheme("HighContrast")
            // Buttons
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.Yellow)
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.Yellow)
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.Black)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Yellow)
            .Set(ListTheme.SelectedIndicator, "► ")
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.White)
            .Set(SplitterTheme.DividerCharacter, "║")
            // ToggleSwitch
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.Black)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.Yellow)
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.Black)
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(128, 128, 0))
            .Set(ToggleSwitchTheme.FocusedBracketForegroundColor, Hex1bColor.Yellow)
            .Set(ToggleSwitchTheme.LeftBracket, "◄ ")
            .Set(ToggleSwitchTheme.RightBracket, " ►")
            .Lock();
    }

    private static Hex1bTheme CreateSunsetTheme()
    {
        return new Hex1bTheme("Sunset")
            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(200, 80, 40))
            // TextBox
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(255, 180, 100))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(180, 60, 30))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.White)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(200, 80, 40))
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(255, 140, 60))
            // ToggleSwitch
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.White)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.FromRgb(200, 80, 40))
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.FromRgb(255, 180, 100))
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(100, 40, 20))
            .Set(ToggleSwitchTheme.FocusedBracketForegroundColor, Hex1bColor.FromRgb(255, 180, 100))
            .Lock();
    }
}
