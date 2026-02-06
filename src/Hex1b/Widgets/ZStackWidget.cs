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
        var popupWidgets = node.Popups.BuildPopupWidgets().ToList();
        allChildren.AddRange(popupWidgets);
        
        // Track whether new popups were added (for focus management)
        var previousChildCount = node.Children.Count;
        var popupStartIndex = Children.Count;
        var previousPopupCount = previousChildCount > popupStartIndex ? previousChildCount - popupStartIndex : 0;
        var currentPopupCount = popupWidgets.Count;
        var newPopupsAdded = currentPopupCount > previousPopupCount;
        
        // Check if the topmost popup was replaced (same or more count, different entry)
        // This handles the case of replacing one popup with another via Pop() + PushAnchored()
        // We should NOT trigger focus management when popups are simply removed
        var topmostPopupReplaced = false;
        var currentTopmostEntry = node.Popups.Entries.Count > 0 ? node.Popups.Entries[^1] : null;
        if (currentTopmostEntry != null && node.LastTopmostPopupEntry != null && 
            !ReferenceEquals(currentTopmostEntry, node.LastTopmostPopupEntry) &&
            currentPopupCount >= previousPopupCount)  // Only if count stayed same or increased
        {
            topmostPopupReplaced = true;
        }
        // Update the tracked topmost entry for next reconcile
        node.LastTopmostPopupEntry = currentTopmostEntry;
        
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
        
        // Update popup entries with their reconciled content nodes for coordinate-aware dismissal
        var popupEntries = node.Popups.Entries;
        for (int i = 0; i < popupEntries.Count; i++)
        {
            var childIndex = popupStartIndex + i;
            if (childIndex < newChildren.Count)
            {
                // The child is a BackdropNode - get its content child for bounds checking
                var backdropNode = newChildren[childIndex] as BackdropNode;
                popupEntries[i].ContentNode = backdropNode?.Child;
            }
        }

        // Focus management: Focus the first focusable in the topmost layer that has focusables
        // This gives overlay content focus priority
        // We need to focus when:
        // 1. The ZStack node is newly created (and parent doesn't manage focus), OR
        // 2. New popups were added (popups ALWAYS take focus, even if parent manages focus), OR
        // 3. The topmost popup was replaced with a different one (e.g., navigating between menus)
        // We should NOT focus when popups are simply removed - let existing focus persist
        var shouldFocusTopmost = 
            (context.IsNew && !context.ParentManagesFocus()) || 
            newPopupsAdded ||
            topmostPopupReplaced;
        
        if (shouldFocusTopmost)
        {
            // First, clear focus on ALL nodes in the tree (recursively)
            // This ensures only the new popup content has focus
            foreach (var child in node.Children)
            {
                ClearFocusRecursive(child);
            }
            
            // Set focus on first focusable in topmost layer
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
    
    /// <summary>
    /// Recursively clears IsFocused on all focusable nodes in the subtree.
    /// </summary>
    private static void ClearFocusRecursive(Hex1bNode node)
    {
        // Clear this node's focus if it's focusable
        if (node.IsFocusable && node.IsFocused)
        {
            ReconcileContext.SetNodeFocus(node, false);
        }
        
        // Recursively process children
        foreach (var child in node.GetChildren())
        {
            ClearFocusRecursive(child);
        }
    }

    internal override Type GetExpectedNodeType() => typeof(ZStackNode);
}
