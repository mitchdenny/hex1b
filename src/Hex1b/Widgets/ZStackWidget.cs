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
    /// Debug: Last focus management state. Used for testing.
    /// </summary>
    public static string? LastFocusDebug { get; internal set; }
    
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
        
        // Also check if the topmost popup changed (same count but different entry)
        // This handles the case of replacing one popup with another via Pop() + PushAnchored()
        var topmostPopupChanged = false;
        var currentTopmostEntry = node.Popups.Entries.Count > 0 ? node.Popups.Entries[^1] : null;
        if (currentTopmostEntry != null && node.LastTopmostPopupEntry != null && 
            !ReferenceEquals(currentTopmostEntry, node.LastTopmostPopupEntry))
        {
            topmostPopupChanged = true;
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
        var shouldFocusTopmost = 
            (context.IsNew && !context.ParentManagesFocus()) || 
            newPopupsAdded ||
            topmostPopupChanged;
        
        // Debug: Track focus management state - only update when shouldFocusTopmost is true
        // to capture the moment focus is supposed to be set
        var debugInfo = $"shouldFocusTopmost={shouldFocusTopmost}, context.IsNew={context.IsNew}, newPopupsAdded={newPopupsAdded}, topmostPopupChanged={topmostPopupChanged}, previousPopupCount={previousPopupCount}, currentPopupCount={currentPopupCount}";
        if (shouldFocusTopmost)
        {
            LastFocusDebug = debugInfo + " [FOCUS TRIGGERED]";
        }
        else
        {
            // Only update if we haven't captured a trigger yet
            LastFocusDebug ??= debugInfo;
        }
            
        if (shouldFocusTopmost)
        {
            // First, clear focus on ALL focusables in the tree
            // This ensures only the new popup content has focus
            var clearedNodes = new List<string>();
            foreach (var child in node.Children)
            {
                foreach (var focusable in child.GetFocusableNodes())
                {
                    if (focusable.IsFocused)
                    {
                        clearedNodes.Add(focusable.GetType().Name);
                        ReconcileContext.SetNodeFocus(focusable, false);
                    }
                }
            }
            
            // Iterate children in reverse (topmost first) to find focusables
            string? focusedNodeType = null;
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var focusables = node.Children[i].GetFocusableNodes().ToList();
                if (focusables.Count > 0)
                {
                    focusedNodeType = focusables[0].GetType().Name;
                    ReconcileContext.SetNodeFocus(focusables[0], true);
                    break;
                }
            }
            
            LastFocusDebug = debugInfo + $" [FOCUS TRIGGERED: cleared=[{string.Join(",", clearedNodes)}], focused={focusedNodeType}]";
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ZStackNode);
}
