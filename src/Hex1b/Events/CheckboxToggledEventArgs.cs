using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for when a checkbox is toggled. The state transition is
/// expressed as a pair of <see cref="CheckboxValue"/> values rather than as
/// references to the underlying <see cref="CheckboxState"/> instance — toggles
/// mutate the existing state in place, so the "previous" instance no longer
/// exists by the time the event fires.
/// </summary>
public sealed class CheckboxToggledEventArgs
{
    /// <summary>
    /// The widget that raised the event.
    /// </summary>
    public CheckboxWidget Widget { get; }

    /// <summary>
    /// The node that raised the event.
    /// </summary>
    public CheckboxNode Node { get; }

    /// <summary>
    /// The input binding context.
    /// </summary>
    public InputBindingActionContext Context { get; }

    /// <summary>
    /// The previous value before toggling.
    /// </summary>
    public CheckboxValue PreviousValue { get; }

    /// <summary>
    /// The new value after toggling.
    /// </summary>
    public CheckboxValue NewValue { get; }

    internal CheckboxToggledEventArgs(
        CheckboxWidget widget,
        CheckboxNode node,
        InputBindingActionContext context,
        CheckboxValue previousValue,
        CheckboxValue newValue)
    {
        Widget = widget;
        Node = node;
        Context = context;
        PreviousValue = previousValue;
        NewValue = newValue;
    }

    internal CheckboxToggledEventArgs(
        CheckboxWidget widget,
        CheckboxNode node,
        InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
        // node.State.Value has already been toggled by CheckboxNode.Toggle(),
        // so it represents the NEW value. Derive the previous value.
        NewValue = node.State.Value;
        PreviousValue = node.State.Value == CheckboxValue.Checked
            ? CheckboxValue.Unchecked
            : CheckboxValue.Checked;
    }
}
