using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="DatePickerWidget"/> instances using the fluent builder API.
/// </summary>
public static class DatePickerExtensions
{
    /// <summary>
    /// Creates a date picker widget with no initial date.
    /// </summary>
    public static DatePickerWidget DatePicker<TParent>(
        this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a date picker widget with an initial selected date.
    /// </summary>
    public static DatePickerWidget DatePicker<TParent>(
        this WidgetContext<TParent> ctx,
        DateOnly initialDate)
        where TParent : Hex1bWidget
        => new() { InitialDate = initialDate };

    /// <summary>
    /// Sets the placeholder text shown when no date is selected.
    /// </summary>
    public static DatePickerWidget Placeholder(this DatePickerWidget widget, string placeholder)
        => widget with { Placeholder = placeholder };

    /// <summary>
    /// Sets the format string for displaying the selected date.
    /// Uses .NET date format strings (e.g. "yyyy-MM-dd", "MMMM d, yyyy").
    /// </summary>
    public static DatePickerWidget Format(this DatePickerWidget widget, string format)
        => widget with { DateFormat = format };

    /// <summary>
    /// Sets the first day of the week for the calendar step.
    /// </summary>
    public static DatePickerWidget FirstDayOfWeek(this DatePickerWidget widget, DayOfWeek firstDay)
        => widget with { FirstDayOfWeek = firstDay };
}
