using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A widget that positions its child relative to an anchor node.
/// Used internally by PopupStack for anchored popups.
/// </summary>
/// <param name="Child">The popup content to position.</param>
/// <param name="AnchorNode">The node to anchor to.</param>
/// <param name="Position">Where to position relative to the anchor.</param>
internal sealed record AnchoredWidget(
    Hex1bWidget Child,
    Hex1bNode AnchorNode,
    AnchorPosition Position) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as AnchoredNode ?? new AnchoredNode();
        
        node.AnchorNode = AnchorNode;
        node.Position = Position;
        
        // Reconcile the child
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        node.Child = await childContext.ReconcileChildAsync(node.Child, Child, node);
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(AnchoredNode);
}
