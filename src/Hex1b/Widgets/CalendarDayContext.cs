namespace Hex1b.Widgets;

/// <summary>
/// Provides contextual information about a specific day in a <see cref="CalendarWidget"/>.
/// Passed to the <see cref="CalendarWidget.DayBuilder"/> callback to allow custom content per day cell.
/// </summary>
/// <param name="Date">The date being rendered.</param>
/// <param name="IsToday">Whether this date is today.</param>
/// <param name="IsSelected">Whether this date is currently selected.</param>
/// <param name="IsWeekend">Whether this date falls on a weekend (Saturday or Sunday).</param>
/// <param name="DayOfWeek">The day of the week for this date.</param>
public sealed record CalendarDayContext(
    DateOnly Date,
    bool IsToday,
    bool IsSelected,
    bool IsWeekend,
    DayOfWeek DayOfWeek);
