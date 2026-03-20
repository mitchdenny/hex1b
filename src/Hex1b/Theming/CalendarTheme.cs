namespace Hex1b.Theming;

/// <summary>
/// Theme elements for <see cref="Hex1b.Widgets.CalendarWidget"/>.
/// </summary>
public static class CalendarTheme
{
    /// <summary>Foreground color for day-of-week header labels.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HeaderForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(HeaderForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Foreground color for regular day numbers.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DayForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(DayForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for regular day numbers.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> DayBackgroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(DayBackgroundColor)}", () => Hex1bColor.Default);

    /// <summary>Foreground color for the current day number.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CurrentDayForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(CurrentDayForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for the current day number.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> CurrentDayBackgroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(CurrentDayBackgroundColor)}", () => Hex1bColor.Gray);

    /// <summary>Foreground color for a hovered day number.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoverForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(HoverForegroundColor)}", () => Hex1bColor.Default);

    /// <summary>Background color for a hovered day number.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> HoverBackgroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(HoverBackgroundColor)}", () => Hex1bColor.Gray);

    /// <summary>Foreground color for the selected day number (inverted).</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(SelectedForegroundColor)}", () => Hex1bColor.Black);

    /// <summary>Background color for the selected day number (inverted).</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> SelectedBackgroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(SelectedBackgroundColor)}", () => Hex1bColor.White);

    /// <summary>Foreground color for weekend day numbers.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> WeekendForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(WeekendForegroundColor)}", () => Hex1bColor.Default);
}
