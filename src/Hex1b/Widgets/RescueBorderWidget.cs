using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A border widget with rescue-specific styling (hardcoded colors).
/// </summary>
internal sealed record RescueBorderWidget(Hex1bWidget Child) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as RescueBorderNode ?? new RescueBorderNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(RescueBorderNode);
}
