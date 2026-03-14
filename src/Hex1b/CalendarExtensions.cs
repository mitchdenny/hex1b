using Hex1b.Events;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="CalendarWidget"/> instances using the fluent builder API.
/// </summary>
public static class CalendarExtensions
{
    /// <summary>
    /// Creates a calendar widget displaying the specified month.
    /// </summary>
    /// <param name="ctx">The parent widget context.</param>
    /// <param name="month">The month to display. Only the Year and Month components are used.</param>
    /// <returns>A configured <see cref="CalendarWidget"/>.</returns>
    public static CalendarWidget Calendar<TParent>(
        this WidgetContext<TParent> ctx,
        DateOnly month)
        where TParent : Hex1bWidget
        => new(month);

    /// <summary>
    /// Creates a calendar widget displaying the current month.
    /// </summary>
    /// <param name="ctx">The parent widget context.</param>
    /// <returns>A configured <see cref="CalendarWidget"/>.</returns>
    public static CalendarWidget Calendar<TParent>(
        this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>
    /// Creates a calendar widget displaying the specified year and month.
    /// </summary>
    /// <param name="ctx">The parent widget context.</param>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1-12).</param>
    /// <returns>A configured <see cref="CalendarWidget"/>.</returns>
    public static CalendarWidget Calendar<TParent>(
        this WidgetContext<TParent> ctx,
        int year, int month)
        where TParent : Hex1bWidget
        => new(new DateOnly(year, month, 1));

    /// <summary>
    /// Sets whether to display the day-of-week header row.
    /// </summary>
    public static CalendarWidget ShowHeader(this CalendarWidget widget, bool show)
        => widget with { ShowHeader = show };

    /// <summary>
    /// Sets the first day of the week for the calendar layout.
    /// </summary>
    public static CalendarWidget FirstDayOfWeek(this CalendarWidget widget, DayOfWeek firstDay)
        => widget with { FirstDayOfWeek = firstDay };

    /// <summary>
    /// Sets the "today" date used for highlighting the current day.
    /// </summary>
    public static CalendarWidget Today(this CalendarWidget widget, DateOnly today)
        => widget with { Today = today };

    /// <summary>
    /// Enables compact mode (no gridlines). Ideal for embedding in a DatePicker.
    /// </summary>
    public static CalendarWidget Compact(this CalendarWidget widget)
        => widget with { IsCompact = true };

    /// <summary>
    /// Provides a callback to build custom content for each day cell.
    /// The callback receives a <see cref="CalendarDayContext"/> with information about the day
    /// and returns an optional widget rendered alongside the day number.
    /// </summary>
    /// <param name="widget">The calendar widget.</param>
    /// <param name="builder">A callback that receives day context and returns optional content.</param>
    /// <returns>A configured <see cref="CalendarWidget"/>.</returns>
    public static CalendarWidget Day(
        this CalendarWidget widget,
        Func<CalendarDayContext, Hex1bWidget?> builder)
        => widget with { DayBuilder = builder };
}
