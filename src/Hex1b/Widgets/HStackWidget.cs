using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record HStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget, IFloatWidgetContainer
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as HStackNode ?? new HStackNode();

        // Create child context with horizontal layout axis
        var childContext = context.WithLayoutAxis(LayoutAxis.Horizontal);

        // Separate flow and float children, reconcile floats
        var widgetToNode = new Dictionary<Hex1bWidget, Hex1bNode>(ReferenceEqualityComparer.Instance);
        var (flowChildren, floatEntries) = await FloatLayoutHelper.ReconcileFloatsAsync(
            Children, node.Floats, childContext, node, widgetToNode);

        // Track children that will be removed (their bounds need clearing)
        var flowCount = flowChildren.Count;
        for (int i = flowCount; i < node.Children.Count; i++)
        {
            var removedChild = node.Children[i];
            if (removedChild.Bounds.Width > 0 && removedChild.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(removedChild.Bounds);
            }
        }
        
        // Reconcile flow children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < flowCount; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var positionedContext = childContext.WithChildPosition(i, flowCount);
            var reconciledChild = await positionedContext.ReconcileChildAsync(existingChild, flowChildren[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
                widgetToNode[flowChildren[i]] = reconciledChild;
            }
        }
        node.Children = newChildren;
        node.Floats = floatEntries;
        node.AllChildrenInOrder = FloatLayoutHelper.BuildDeclarationOrder(Children, floatEntries, widgetToNode);

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
