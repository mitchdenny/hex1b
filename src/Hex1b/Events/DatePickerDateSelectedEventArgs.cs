using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for date picker selection events.
/// </summary>
public sealed class DatePickerDateSelectedEventArgs : WidgetEventArgs<DatePickerWidget, DatePickerNode>
{
    /// <summary>
    /// The selected date.
    /// </summary>
    public DateOnly SelectedDate { get; }

    internal DatePickerDateSelectedEventArgs(
        DatePickerWidget widget,
        DatePickerNode node,
        InputBindingActionContext context,
        DateOnly selectedDate)
        : base(widget, node, context)
    {
        SelectedDate = selectedDate;
    }
}
