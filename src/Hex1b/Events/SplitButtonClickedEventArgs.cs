using Hex1b.Input;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for split button click events.
/// </summary>
public sealed class SplitButtonClickedEventArgs : EventArgs
{
    internal SplitButtonClickedEventArgs(
        Widgets.SplitButtonWidget widget,
        SplitButtonNode node,
        InputBindingActionContext context)
    {
        Widget = widget;
        Node = node;
        Context = context;
    }

    /// <summary>
    /// The split button widget that was clicked.
    /// </summary>
    public Widgets.SplitButtonWidget Widget { get; }

    /// <summary>
    /// The split button node that was clicked.
    /// </summary>
    public SplitButtonNode Node { get; }

    /// <summary>
    /// The input context for this event.
    /// </summary>
    public InputBindingActionContext Context { get; }
}
