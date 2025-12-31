namespace Hex1b.Theming;

/// <summary>
/// Theme elements for Rescue widgets.
/// These define the styling for the error fallback UI.
/// </summary>
public static class RescueTheme
{
    // Background and text colors
    public static readonly Hex1bThemeElement<Hex1bColor> BackgroundColor =
        new($"{nameof(RescueTheme)}.{nameof(BackgroundColor)}", () => Hex1bColor.FromRgb(40, 0, 0));

    public static readonly Hex1bThemeElement<Hex1bColor> ForegroundColor =
        new($"{nameof(RescueTheme)}.{nameof(ForegroundColor)}", () => Hex1bColor.FromRgb(255, 200, 200));

    // Border colors
    public static readonly Hex1bThemeElement<Hex1bColor> BorderColor =
        new($"{nameof(RescueTheme)}.{nameof(BorderColor)}", () => Hex1bColor.FromRgb(255, 80, 80));

    public static readonly Hex1bThemeElement<Hex1bColor> TitleColor =
        new($"{nameof(RescueTheme)}.{nameof(TitleColor)}", () => Hex1bColor.FromRgb(255, 255, 255));

    // Border characters (double-line for distinctive look)
    public static readonly Hex1bThemeElement<string> TopLeftCorner =
        new($"{nameof(RescueTheme)}.{nameof(TopLeftCorner)}", () => "╔");

    public static readonly Hex1bThemeElement<string> TopRightCorner =
        new($"{nameof(RescueTheme)}.{nameof(TopRightCorner)}", () => "╗");

    public static readonly Hex1bThemeElement<string> BottomLeftCorner =
        new($"{nameof(RescueTheme)}.{nameof(BottomLeftCorner)}", () => "╚");

    public static readonly Hex1bThemeElement<string> BottomRightCorner =
        new($"{nameof(RescueTheme)}.{nameof(BottomRightCorner)}", () => "╝");

    public static readonly Hex1bThemeElement<string> HorizontalLine =
        new($"{nameof(RescueTheme)}.{nameof(HorizontalLine)}", () => "═");

    public static readonly Hex1bThemeElement<string> VerticalLine =
        new($"{nameof(RescueTheme)}.{nameof(VerticalLine)}", () => "║");

    // Separator characters (double-line to match border)
    public static readonly Hex1bThemeElement<string> SeparatorHorizontalChar =
        new($"{nameof(RescueTheme)}.{nameof(SeparatorHorizontalChar)}", () => "═");

    public static readonly Hex1bThemeElement<string> SeparatorVerticalChar =
        new($"{nameof(RescueTheme)}.{nameof(SeparatorVerticalChar)}", () => "║");

    public static readonly Hex1bThemeElement<Hex1bColor> SeparatorColor =
        new($"{nameof(RescueTheme)}.{nameof(SeparatorColor)}", () => Hex1bColor.FromRgb(255, 80, 80));

    // Button colors
    public static readonly Hex1bThemeElement<Hex1bColor> ButtonForegroundColor =
        new($"{nameof(RescueTheme)}.{nameof(ButtonForegroundColor)}", () => Hex1bColor.FromRgb(255, 200, 200));

    public static readonly Hex1bThemeElement<Hex1bColor> ButtonBackgroundColor =
        new($"{nameof(RescueTheme)}.{nameof(ButtonBackgroundColor)}", () => Hex1bColor.FromRgb(80, 30, 30));

    public static readonly Hex1bThemeElement<Hex1bColor> ButtonFocusedForegroundColor =
        new($"{nameof(RescueTheme)}.{nameof(ButtonFocusedForegroundColor)}", () => Hex1bColor.FromRgb(255, 255, 255));

    public static readonly Hex1bThemeElement<Hex1bColor> ButtonFocusedBackgroundColor =
        new($"{nameof(RescueTheme)}.{nameof(ButtonFocusedBackgroundColor)}", () => Hex1bColor.FromRgb(255, 80, 80));

    // Text colors for different parts
    public static readonly Hex1bThemeElement<Hex1bColor> ErrorTypeColor =
        new($"{nameof(RescueTheme)}.{nameof(ErrorTypeColor)}", () => Hex1bColor.FromRgb(255, 255, 100));

    public static readonly Hex1bThemeElement<Hex1bColor> StackTraceColor =
        new($"{nameof(RescueTheme)}.{nameof(StackTraceColor)}", () => Hex1bColor.FromRgb(180, 180, 180));

    public static readonly Hex1bThemeElement<Hex1bColor> PhaseColor =
        new($"{nameof(RescueTheme)}.{nameof(PhaseColor)}", () => Hex1bColor.FromRgb(100, 200, 255));
}
