using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A context for building the child of a <see cref="DropTargetWidget"/>.
/// Provides access to the current activation state.
/// </summary>
public sealed class DropTargetContext : WidgetContext<DropTargetWidget>
{
    private readonly DropTargetNode _node;

    internal DropTargetContext(DropTargetNode node)
    {
        _node = node;
    }

    /// <summary>
    /// Whether this drop target is currently the nearest to the drag cursor.
    /// </summary>
    public bool IsActive => _node.IsActive;

    /// <summary>
    /// The drag data of the item currently being dragged. Null if no drag is active.
    /// </summary>
    public object? DragData => _node.DragData;
}
