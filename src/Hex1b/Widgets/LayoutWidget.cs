using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A layout widget that provides clipping and rendering assistance to its children.
/// Children that are aware of layout can query whether characters should be rendered.
/// </summary>
public sealed record LayoutWidget(Hex1bWidget Child, ClipMode ClipMode = ClipMode.Clip) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as LayoutNode ?? new LayoutNode();
        node.ClipMode = ClipMode;
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(LayoutNode);
}
