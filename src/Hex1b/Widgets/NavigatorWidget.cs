using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A navigator widget that provides stack-based navigation for building 
/// wizard-style flows and drill-down experiences.
/// 
/// The navigator maintains a stack of routes. Use the NavigatorState to:
/// - Push: Navigate to a new screen (drill down)
/// - Pop: Go back to the previous screen
/// - PopToRoot: Return to the starting screen (complete wizard)
/// - Replace: Swap the current screen without adding to history
/// </summary>
[Experimental("HEX1B001")]
public sealed record NavigatorWidget(NavigatorState State) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as NavigatorNode ?? new NavigatorNode();
        node.State = State;

        // Detect if the route has changed
        var newRouteId = State.CurrentRoute.Id;
        var routeChanged = node.CurrentRouteId != newRouteId;
        
        // Check if we have a pending focus restore (from a pop)
        var pendingFocusRestore = State.PendingFocusRestore;
        // Check if we need to save focus to a previous entry (from a push)
        var entryToSaveFocusTo = State.EntryToSaveFocusTo;
        State.ClearPendingFocusRestore();
        
        node.CurrentRouteId = newRouteId;

        // If route changed, save focus index to the previous entry and clear focus from old child
        if (routeChanged && node.CurrentChild != null)
        {
            // Save the current focus index to the entry we're navigating away from (only on push)
            if (entryToSaveFocusTo != null)
            {
                var oldFocusables = node.CurrentChild.GetFocusableNodes().ToList();
                Console.Error.WriteLine($"[Navigator] Route changed, saving focus. Old focusables count: {oldFocusables.Count}");
                bool foundFocused = false;
                for (int i = 0; i < oldFocusables.Count; i++)
                {
                    var isFocused = ReconcileContext.IsNodeFocused(oldFocusables[i]);
                    Console.Error.WriteLine($"[Navigator]   [{i}] {oldFocusables[i].GetType().Name}: IsFocused={isFocused}");
                    if (isFocused)
                    {
                        entryToSaveFocusTo.SavedFocusIndex = i;
                        foundFocused = true;
                        Console.Error.WriteLine($"[Navigator] Saved focus index: {i}");
                        break;
                    }
                }
                if (!foundFocused)
                {
                    Console.Error.WriteLine($"[Navigator] WARNING: No focused element found!");
                }
            }

            foreach (var focusable in node.CurrentChild.GetFocusableNodes())
            {
                ReconcileContext.SetNodeFocus(focusable, false);
            }
            // Force creation of new child by not passing existing
            node.CurrentChild = null;
        }

        // Build the current route's widget and reconcile it as the child
        var currentWidget = State.BuildCurrentWidget();
        node.CurrentChild = await context.ReconcileChildAsync(node.CurrentChild, currentWidget, node);

        // Set focus based on whether we're returning from pop or navigating forward
        if (context.IsNew || routeChanged)
        {
            var focusables = node.GetFocusableNodes().ToList();
            Console.Error.WriteLine($"[Navigator] Setting focus. Focusables count: {focusables.Count}, pendingFocusRestore: {pendingFocusRestore}");
            if (focusables.Count > 0)
            {
                // Clear all existing focus first
                foreach (var focusable in focusables)
                {
                    ReconcileContext.SetNodeFocus(focusable, false);
                }
                
                int focusIndex = 0;
                
                // If returning from pop, restore saved focus index
                if (pendingFocusRestore.HasValue && pendingFocusRestore.Value < focusables.Count)
                {
                    focusIndex = pendingFocusRestore.Value;
                    Console.Error.WriteLine($"[Navigator] Restoring focus to index: {focusIndex}");
                }
                
                ReconcileContext.SetNodeFocus(focusables[focusIndex], true);
                Console.Error.WriteLine($"[Navigator] Set focus to: {focusables[focusIndex].GetType().Name}");
                
                // After setting focus, sync the internal focus index on container nodes
                if (node.CurrentChild != null)
                {
                    ReconcileContext.SyncContainerFocusIndices(node.CurrentChild);
                }
            }
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(NavigatorNode);
}
