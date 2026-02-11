using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for interactable click events.
/// Fired when the interactable is activated via Enter, Space, or mouse click.
/// </summary>
public sealed class InteractableClickedEventArgs : WidgetEventArgs<InteractableWidget, InteractableNode>
{
    public InteractableClickedEventArgs(InteractableWidget widget, InteractableNode node, InputBindingActionContext context)
        : base(widget, node, context)
    {
    }
}
