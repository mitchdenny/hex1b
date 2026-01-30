using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays the first child whose condition evaluates to true.
/// Conditions are evaluated during layout with the available size from parent constraints.
/// </summary>
/// <param name="Branches">The list of conditional widgets to evaluate. The first matching branch is displayed.</param>
public sealed record ResponsiveWidget(IReadOnlyList<ConditionalWidget> Branches) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ResponsiveNode ?? new ResponsiveNode();
        node.Branches = Branches;

        // Reconcile child nodes for each branch
        var newChildNodes = new List<Hex1bNode?>();
        for (int i = 0; i < Branches.Count; i++)
        {
            var existingChild = i < node.ChildNodes.Count ? node.ChildNodes[i] : null;
            var reconciledChild = await context.ReconcileChildAsync(existingChild, Branches[i].Content, node);
            newChildNodes.Add(reconciledChild);
        }
        node.ChildNodes = newChildNodes;

        // Note: We don't set initial focus here. Focus is managed by the FocusRing
        // which will call EnsureFocus() to set focus on the first focusable if needed.

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ResponsiveNode);
}
