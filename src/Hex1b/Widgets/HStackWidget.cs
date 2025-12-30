using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record HStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as HStackNode ?? new HStackNode();

        // Create child context with horizontal layout axis
        var childContext = context.WithLayoutAxis(LayoutAxis.Horizontal);
        
        // Track children that will be removed (their bounds need clearing)
        for (int i = Children.Count; i < node.Children.Count; i++)
        {
            var removedChild = node.Children[i];
            if (removedChild.Bounds.Width > 0 && removedChild.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(removedChild.Bounds);
            }
        }
        
        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < Children.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = childContext.ReconcileChild(existingChild, Children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

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

    internal override Type GetExpectedNodeType() => typeof(HStackNode);
}
