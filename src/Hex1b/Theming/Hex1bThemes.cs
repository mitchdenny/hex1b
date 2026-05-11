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
            // Buttons — resting chip is a slightly brighter blue-grey than the
            // input-field family so buttons read as sitting above input surfaces.
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(25, 40, 60))
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 100, 180))
            // SplitButton arrow region — half-shade darker than the primary
            // chip; focus drops a tone below the focused button so the
            // dropdown affordance stays distinguishable inside the bright chip.
            .Set(SplitButtonTheme.ArrowBackgroundColor, Hex1bColor.FromRgb(20, 32, 48))
            .Set(SplitButtonTheme.FocusedArrowForegroundColor, Hex1bColor.White)
            .Set(SplitButtonTheme.FocusedArrowBackgroundColor, Hex1bColor.FromRgb(0, 80, 140))
            // TextBox — deeper blue-grey for resting, mid blue-grey for focused.
            .Set(TextBoxTheme.FillBackgroundColor, Hex1bColor.FromRgb(15, 25, 40))
            .Set(TextBoxTheme.FocusedFillBackgroundColor, Hex1bColor.FromRgb(20, 40, 70))
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(100, 200, 255))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(0, 80, 140))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.White)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 100, 180))
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(0, 120, 200))
            // ToggleSwitch — unselected chips share the TextBox resting tone so
            // the toggle reads as part of the same input-surface family.
            .Set(ToggleSwitchTheme.UnselectedBackgroundColor, Hex1bColor.FromRgb(15, 25, 40))
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.White)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.FromRgb(0, 100, 180))
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.FromRgb(100, 200, 255))
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(0, 50, 90))
            .Lock();
    }

    private static Hex1bTheme CreateHighContrastTheme()
    {
        return new Hex1bTheme("HighContrast")
            // Buttons — resting chip stays a stark dark grey so the bright
            // yellow focus state lands as a maximum-contrast invert.
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(40, 40, 40))
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
            // SplitButton arrow region — black resting chip, slightly dimmed
            // yellow focus so the divider/arrow stays separable from the
            // primary action even at peak contrast.
            .Set(SplitButtonTheme.ArrowForegroundColor, Hex1bColor.White)
            .Set(SplitButtonTheme.ArrowBackgroundColor, Hex1bColor.FromRgb(20, 20, 20))
            .Set(SplitButtonTheme.FocusedArrowForegroundColor, Hex1bColor.Black)
            .Set(SplitButtonTheme.FocusedArrowBackgroundColor, Hex1bColor.FromRgb(180, 180, 0))
            // TextBox — deepest black resting fill; subtle yellow tint when
            // focused so the field advertises focus without losing its
            // identity as a recessed input surface.
            .Set(TextBoxTheme.FillBackgroundColor, Hex1bColor.FromRgb(20, 20, 20))
            .Set(TextBoxTheme.FocusedFillBackgroundColor, Hex1bColor.FromRgb(50, 50, 0))
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
            // ToggleSwitch — unselected chips share the TextBox black so the
            // toggle strip reads as the same input-surface family.
            .Set(ToggleSwitchTheme.UnselectedBackgroundColor, Hex1bColor.FromRgb(20, 20, 20))
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.Black)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.Yellow)
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.Black)
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(128, 128, 0))
            .Lock();
    }

    private static Hex1bTheme CreateSunsetTheme()
    {
        return new Hex1bTheme("Sunset")
            // Buttons — slightly brighter warm dark than the input-field
            // family so buttons sit above input surfaces with a sunlit feel.
            .Set(ButtonTheme.BackgroundColor, Hex1bColor.FromRgb(55, 35, 25))
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.White)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(200, 80, 40))
            // SplitButton arrow region — half-shade darker warm tone resting,
            // dimmed orange when focused so the dropdown affordance stays
            // distinguishable inside the bright chip.
            .Set(SplitButtonTheme.ArrowBackgroundColor, Hex1bColor.FromRgb(45, 28, 22))
            .Set(SplitButtonTheme.FocusedArrowForegroundColor, Hex1bColor.White)
            .Set(SplitButtonTheme.FocusedArrowBackgroundColor, Hex1bColor.FromRgb(170, 60, 30))
            // TextBox — deep warm dark for resting, slightly brighter warm
            // when focused.
            .Set(TextBoxTheme.FillBackgroundColor, Hex1bColor.FromRgb(40, 25, 20))
            .Set(TextBoxTheme.FocusedFillBackgroundColor, Hex1bColor.FromRgb(60, 35, 25))
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(255, 180, 100))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(180, 60, 30))
            .Set(TextBoxTheme.SelectionForegroundColor, Hex1bColor.White)
            // List
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.White)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(200, 80, 40))
            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(255, 140, 60))
            // ToggleSwitch — unselected chips match the TextBox resting tone
            // so the toggle strip belongs to the same warm input-surface family.
            .Set(ToggleSwitchTheme.UnselectedBackgroundColor, Hex1bColor.FromRgb(40, 25, 20))
            .Set(ToggleSwitchTheme.FocusedSelectedForegroundColor, Hex1bColor.White)
            .Set(ToggleSwitchTheme.FocusedSelectedBackgroundColor, Hex1bColor.FromRgb(200, 80, 40))
            .Set(ToggleSwitchTheme.UnfocusedSelectedForegroundColor, Hex1bColor.FromRgb(255, 180, 100))
            .Set(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor, Hex1bColor.FromRgb(100, 40, 20))
            .Lock();
    }
}
