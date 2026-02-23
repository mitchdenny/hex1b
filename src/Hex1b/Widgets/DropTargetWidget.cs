using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that marks an insertion point inside a <see cref="DroppableWidget"/>.
/// When a drag operation is active, the nearest DropTarget to the cursor becomes active
/// and its builder callback can render a visual indicator (e.g., a separator line).
/// When inactive, the node measures as zero height so it doesn't create gaps.
/// </summary>
/// <param name="Id">A unique identifier for this drop target within its parent droppable.</param>
/// <param name="Builder">A builder that receives a <see cref="DropTargetContext"/> and returns the content to render.</param>
public sealed record DropTargetWidget(string Id, Func<DropTargetContext, Hex1bWidget> Builder) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DropTargetNode ?? new DropTargetNode();
        node.TargetId = Id;

        var ctx = new DropTargetContext(node);
        var childWidget = Builder(ctx);
        node.Child = await context.ReconcileChildAsync(node.Child, childWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(DropTargetNode);
}
