using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A context for building the child tree of a <see cref="DroppableWidget"/>.
/// Provides access to the current drag-hover state from the underlying node.
/// </summary>
public sealed class DroppableContext : WidgetContext<DroppableWidget>
{
    private readonly DroppableNode _node;

    internal DroppableContext(DroppableNode node)
    {
        _node = node;
    }

    /// <summary>
    /// Whether a dragged item is currently hovering over this drop target.
    /// </summary>
    public bool IsHoveredByDrag => _node.IsHoveredByDrag;

    /// <summary>
    /// Whether the currently hovering dragged item passes the Accept predicate.
    /// False if no drag is hovering.
    /// </summary>
    public bool CanAcceptDrag => _node.CanAcceptDrag;

    /// <summary>
    /// The drag data of the item currently hovering over this target.
    /// Null if no drag is hovering.
    /// </summary>
    public object? HoveredDragData => _node.HoveredDragData;
}
