using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for slider value change events.
/// </summary>
/// <remarks>
/// This event is fired for all input types: keyboard, click, drag, and scroll wheel.
/// All events include the full <see cref="InputBindingActionContext"/> for focus navigation
/// and other app-level operations.
/// </remarks>
/// <seealso cref="SliderWidget"/>
/// <seealso cref="SliderNode"/>
public sealed class SliderValueChangedEventArgs : WidgetEventArgs<SliderWidget, SliderNode>
{
    /// <summary>
    /// The new value of the slider.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// The previous value before the change.
    /// </summary>
    public double PreviousValue { get; }

    /// <summary>
    /// The minimum value of the slider range.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// The maximum value of the slider range.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// The current value as a percentage (0.0 to 1.0) of the range.
    /// </summary>
    public double Percentage => Maximum > Minimum 
        ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0.0, 1.0) 
        : 0.0;

    internal SliderValueChangedEventArgs(
        SliderWidget widget,
        SliderNode node,
        InputBindingActionContext context,
        double value,
        double previousValue,
        double minimum,
        double maximum)
        : base(widget, node, context)
    {
        Value = value;
        PreviousValue = previousValue;
        Minimum = minimum;
        Maximum = maximum;
    }
}
