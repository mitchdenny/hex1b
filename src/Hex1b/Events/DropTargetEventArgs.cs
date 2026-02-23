using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for a drop on a specific <see cref="DropTargetWidget"/> within a <see cref="DroppableWidget"/>.
/// Contains the target ID identifying which insertion point received the drop.
/// </summary>
public sealed class DropTargetEventArgs : WidgetEventArgs<DroppableWidget, DroppableNode>
{
    /// <summary>
    /// The ID of the <see cref="DropTargetWidget"/> that received the drop.
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// The reference data supplied by the draggable source.
    /// </summary>
    public object DragData { get; }

    /// <summary>
    /// The draggable node that initiated the drag.
    /// </summary>
    public DraggableNode Source { get; }

    public DropTargetEventArgs(
        DroppableWidget widget,
        DroppableNode node,
        InputBindingActionContext context,
        string targetId,
        object dragData,
        DraggableNode source)
        : base(widget, node, context)
    {
        TargetId = targetId;
        DragData = dragData;
        Source = source;
    }
}
