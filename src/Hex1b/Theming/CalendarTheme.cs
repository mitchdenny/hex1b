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

    /// <summary>Foreground color for the "today" day number.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TodayForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(TodayForegroundColor)}", () => Hex1bColor.White);

    /// <summary>Background color for the "today" day number.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> TodayBackgroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(TodayBackgroundColor)}", () => Hex1bColor.Blue);

    /// <summary>Foreground color for weekend day numbers.</summary>
    public static readonly Hex1bThemeElement<Hex1bColor> WeekendForegroundColor =
        new($"{nameof(CalendarTheme)}.{nameof(WeekendForegroundColor)}", () => Hex1bColor.Default);
}
