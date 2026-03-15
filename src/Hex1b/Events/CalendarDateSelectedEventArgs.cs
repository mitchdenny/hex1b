using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for calendar date selection events.
/// </summary>
public sealed class CalendarDateSelectedEventArgs : WidgetEventArgs<CalendarWidget, CalendarNode>
{
    /// <summary>
    /// The selected date.
    /// </summary>
    public DateOnly SelectedDate { get; }

    /// <summary>
    /// The 1-based day of the month that was selected.
    /// </summary>
    public int Day { get; }

    public CalendarDateSelectedEventArgs(
        CalendarWidget widget,
        CalendarNode node,
        InputBindingActionContext context,
        DateOnly selectedDate)
        : base(widget, node, context)
    {
        SelectedDate = selectedDate;
        Day = selectedDate.Day;
    }
}
