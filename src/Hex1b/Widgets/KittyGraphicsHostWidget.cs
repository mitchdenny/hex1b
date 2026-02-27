using Hex1b.Kgp;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Internal passthrough widget that hosts the <see cref="KgpImageCache"/> for
/// descendant <see cref="KittyGraphicsNode"/>s. Injected automatically by
/// <see cref="Hex1bApp"/> when KGP is enabled.
/// </summary>
internal sealed record KittyGraphicsHostWidget(Hex1bWidget Child) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as KittyGraphicsHostNode ?? new KittyGraphicsHostNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(KittyGraphicsHostNode);
}
