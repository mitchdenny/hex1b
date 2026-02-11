using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A context for building the child tree of an <see cref="InteractableWidget"/>.
/// Provides access to current focus and hover state from the underlying node.
/// </summary>
/// <remarks>
/// This context is created during reconciliation (not during widget building),
/// so <see cref="IsFocused"/> and <see cref="IsHovered"/> reflect the node's
/// current state. On first render, the node is new so both default to false.
/// </remarks>
public sealed class InteractableContext : WidgetContext<InteractableWidget>
{
    private readonly InteractableNode _node;

    internal InteractableContext(InteractableNode node)
    {
        _node = node;
    }

    /// <summary>
    /// Whether the interactable area currently has focus.
    /// </summary>
    public bool IsFocused => _node.IsFocused;

    /// <summary>
    /// Whether the mouse is currently hovering over the interactable area.
    /// </summary>
    public bool IsHovered => _node.IsHovered;
}
