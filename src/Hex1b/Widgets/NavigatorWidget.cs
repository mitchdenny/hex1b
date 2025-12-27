using System.Diagnostics.CodeAnalysis;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A navigator widget that provides stack-based navigation for building 
/// wizard-style flows and drill-down experiences.
/// </summary>
/// <param name="State">The navigator state that manages the navigation stack and current route.</param>
/// <remarks>
/// <para>
/// The Navigator maintains a stack of routes where each route represents a screen or page
/// in your application. It automatically manages:
/// </para>
/// <list type="bullet">
/// <item><description>Navigation history (back stack)</description></item>
/// <item><description>Focus preservation when navigating back</description></item>
/// <item><description>Lifecycle of route widgets</description></item>
/// </list>
/// <para>
/// Use the <see cref="NavigatorState"/> to control navigation:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="NavigatorState.Push(NavigatorRoute)"/>: Navigate to a new screen (drill down)</description></item>
/// <item><description><see cref="NavigatorState.Pop"/>: Go back to the previous screen</description></item>
/// <item><description><see cref="NavigatorState.PopToRoot"/>: Return to the starting screen (complete wizard)</description></item>
/// <item><description><see cref="NavigatorState.Replace(NavigatorRoute)"/>: Swap the current screen without adding to history</description></item>
/// <item><description><see cref="NavigatorState.Reset(NavigatorRoute)"/>: Clear all history and start with a new root</description></item>
/// </list>
/// <para>
/// The Navigator preserves focus state when navigating. When you push a new route, it saves
/// which element had focus. When you pop back, it restores focus to that same element.
/// </para>
/// <para>
/// <strong>Note:</strong> Navigator is marked as experimental with diagnostic ID HEX1B001.
/// Use <c>#pragma warning disable HEX1B001</c> to suppress the experimental warning.
/// </para>
/// </remarks>
/// <example>
/// <para>Simple wizard flow:</para>
/// <code>
/// // Create navigator state with root route
/// var navigator = new NavigatorState(
///     new NavigatorRoute("welcome", nav => BuildWelcomeScreen(nav))
/// );
/// 
/// // Build the navigator widget
/// var widget = ctx.Navigator(navigator);
/// 
/// // Navigate forward from within a route
/// navigator.Push("step2", nav => BuildStep2Screen(nav));
/// 
/// // Navigate back
/// navigator.Pop();
/// 
/// // Complete the wizard
/// navigator.PopToRoot();
/// </code>
/// <para>Master-detail with drill-down:</para>
/// <code>
/// // Root route shows list of customers
/// var nav = new NavigatorState(
///     new NavigatorRoute("customers", n => 
///         ctx.VStack(v => [
///             v.List(customerNames),
///             v.Button("View Details").OnClick(_ => 
///                 n.Push("detail", _ => BuildCustomerDetail()))
///         ])
///     )
/// );
/// </code>
/// <para>Conditional routing (splash → home or first-run):</para>
/// <code>
/// var nav = new NavigatorState(
///     new NavigatorRoute("splash", n => {
///         if (hasExistingData) {
///             return BuildHomeScreen(n);
///         }
///         return BuildFirstRunScreen(n);
///     })
/// );
/// </code>
/// </example>
/// <seealso cref="NavigatorState"/>
/// <seealso cref="NavigatorRoute"/>
/// <seealso cref="NavigatorExtensions"/>
[Experimental("HEX1B001")]
public sealed record NavigatorWidget(NavigatorState State) : Hex1bWidget
{
    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
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
        node.CurrentChild = context.ReconcileChild(node.CurrentChild, currentWidget, node);

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
