using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A widget that positions its child at absolute screen coordinates.
/// Used internally for drag ghost overlays that follow the mouse cursor.
/// </summary>
internal sealed record DragOverlayWidget(
    Hex1bWidget Child,
    int CursorX,
    int CursorY) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DragOverlayNode ?? new DragOverlayNode();

        node.CursorX = CursorX;
        node.CursorY = CursorY;

        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        node.Child = await childContext.ReconcileChildAsync(node.Child, Child, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(DragOverlayNode);
}
