using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A container that stacks children on the Z-axis (depth).
/// All children occupy the same space, with later children rendering on top of earlier ones.
/// This is useful for overlays, floating panels, menus, and modal dialogs.
/// </summary>
/// <param name="Children">The child widgets to stack. First child is at the bottom, last is on top.</param>
public sealed record ZStackWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget
{
    /// <summary>
    /// The clipping scope for this ZStack's content.
    /// Defaults to parent bounds.
    /// </summary>
    internal ClipScope ClipScopeValue { get; init; } = ClipScope.Parent;
    
    /// <summary>
    /// Clips content to the parent's bounds. This is the default behavior.
    /// </summary>
    public ZStackWidget ClipToParent() => this with { ClipScopeValue = ClipScope.Parent };
    
    /// <summary>
    /// Allows content to render to the full screen without clipping.
    /// Useful for popups that should escape their container bounds.
    /// </summary>
    public ZStackWidget ClipToScreen() => this with { ClipScopeValue = ClipScope.Screen };
    
    /// <summary>
    /// Clips content to a specific widget's bounds.
    /// </summary>
    /// <param name="widget">The widget whose bounds define the clip region.</param>
    public ZStackWidget ClipTo(Hex1bWidget widget) => this with { ClipScopeValue = ClipScope.Widget(widget) };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ZStackNode ?? new ZStackNode();
        
        // Set the clip scope on the node
        node.ClipScopeValue = ClipScopeValue;

        // Create child context - ZStack doesn't have a layout axis, children fill available space
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical); // Use vertical as default
        
        // Build the complete list of children: explicit children + popup children
        var allChildren = Children.ToList();
        allChildren.AddRange(node.Popups.BuildPopupWidgets());
        
        // Track children that will be removed (their bounds need clearing)
        for (int i = allChildren.Count; i < node.Children.Count; i++)
        {
            var removedChild = node.Children[i];
            if (removedChild.Bounds.Width > 0 && removedChild.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(removedChild.Bounds);
            }
        }
        
        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < allChildren.Count; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var reconciledChild = await childContext.ReconcileChildAsync(existingChild, allChildren[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

        // Focus management: Focus the first focusable in the topmost layer that has focusables
        // This gives overlay content focus priority
        if (context.IsNew && !context.ParentManagesFocus())
        {
            // Iterate children in reverse (topmost first) to find focusables
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var focusables = node.Children[i].GetFocusableNodes().ToList();
                if (focusables.Count > 0)
                {
                    ReconcileContext.SetNodeFocus(focusables[0], true);
                    break;
                }
            }
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ZStackNode);
}
