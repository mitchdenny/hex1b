using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Internal widget used to render menu popup content.
/// This is pushed to the PopupStack when a menu is opened.
/// </summary>
/// <param name="OwnerNode">The MenuNode that owns this popup.</param>
internal sealed record MenuPopupWidget(MenuNode OwnerNode) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuPopupNode ?? new MenuPopupNode();
        node.OwnerNode = OwnerNode;
        
        // Create children during reconciliation so they're available for focus ring
        node.ReconcileChildNodes();
        
        node.MarkDirty();
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuPopupNode);
}
