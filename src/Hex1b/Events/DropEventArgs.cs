using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for a drop event on a <see cref="DroppableWidget"/>.
/// Contains the drag data from the source, the source node, and the local
/// drop position relative to the droppable target's bounds.
/// </summary>
public sealed class DropEventArgs : WidgetEventArgs<DroppableWidget, DroppableNode>
{
    /// <summary>
    /// The reference data supplied by the draggable source.
    /// </summary>
    public object DragData { get; }

    /// <summary>
    /// The draggable node that initiated the drag.
    /// </summary>
    public DraggableNode Source { get; }

    /// <summary>
    /// The X position of the drop relative to the droppable target's bounds.
    /// </summary>
    public int LocalX { get; }

    /// <summary>
    /// The Y position of the drop relative to the droppable target's bounds.
    /// </summary>
    public int LocalY { get; }

    public DropEventArgs(
        DroppableWidget widget,
        DroppableNode node,
        InputBindingActionContext context,
        object dragData,
        DraggableNode source,
        int localX,
        int localY)
        : base(widget, node, context)
    {
        DragData = dragData;
        Source = source;
        LocalX = localX;
        LocalY = localY;
    }
}
