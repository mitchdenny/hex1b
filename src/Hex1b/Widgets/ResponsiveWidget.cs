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

        // Set initial focus on the first focusable node in each branch.
        // Since we don't know which branch will be active until Measure(),
        // we pre-set focus on all branches' first focusable nodes.
        if (context.IsNew)
        {
            foreach (var child in newChildNodes)
            {
                if (child != null)
                {
                    var firstFocusable = child.GetFocusableNodes().FirstOrDefault();
                    if (firstFocusable != null)
                    {
                        ReconcileContext.SetNodeFocus(firstFocusable, true);
                    }
                }
            }
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ResponsiveNode);
}
