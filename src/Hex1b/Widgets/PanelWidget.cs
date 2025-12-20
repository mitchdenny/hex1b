using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that provides a styled background for its child content.
/// </summary>
public sealed record PanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as PanelNode ?? new PanelNode();
        node.Child = context.ReconcileChild(node.Child, Child, node);
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(PanelNode);
}
