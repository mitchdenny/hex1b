using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for when a checkbox is toggled.
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
    /// The previous state before toggling.
    /// </summary>
    public CheckboxState PreviousState { get; }

    /// <summary>
    /// The new state after toggling.
    /// </summary>
    public CheckboxState NewState { get; }

    internal CheckboxToggledEventArgs(
        CheckboxWidget widget,
        CheckboxNode node,
        InputBindingActionContext context,
        CheckboxState previousState,
        CheckboxState newState)
    {
        Widget = widget;
        Node = node;
        Context = context;
        PreviousState = previousState;
        NewState = newState;
    }

    internal CheckboxToggledEventArgs(
        CheckboxWidget widget,
        CheckboxNode node,
        InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
        PreviousState = node.State;
        // Toggle: Checked -> Unchecked, anything else -> Checked
        NewState = node.State == CheckboxState.Checked ? CheckboxState.Unchecked : CheckboxState.Checked;
    }
}
