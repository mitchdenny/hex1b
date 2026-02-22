using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A context for building the child tree of a <see cref="DraggableWidget"/>.
/// Provides access to the current drag state from the underlying node.
/// </summary>
public sealed class DraggableContext : WidgetContext<DraggableWidget>
{
    private readonly DraggableNode _node;

    internal DraggableContext(DraggableNode node)
    {
        _node = node;
    }

    /// <summary>
    /// Whether this draggable source is currently being dragged.
    /// </summary>
    public bool IsDragging => _node.IsDragging;

    /// <summary>
    /// The reference data associated with this draggable item.
    /// </summary>
    public object DragData => _node.DragData;
}
