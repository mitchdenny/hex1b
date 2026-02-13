using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that provides identity-anchored scope for its child subtree.
/// The state object's reference identity determines which node is reused across
/// reconciliation frames, enabling state preservation across list reorders.
/// </summary>
/// <remarks>
/// <para>
/// The builder is deferred to reconciliation time (like <see cref="InteractableWidget"/>),
/// so the <see cref="StatePanelContext"/> has access to the resolved node's current state.
/// </para>
/// <para>
/// When nested under another StatePanelNode, identity resolution uses the ancestor's
/// registry dictionary keyed by reference equality. Without an ancestor, falls back to
/// positional matching with a state key reference check.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.StatePanel(myViewModel, sp =>
///     sp.Text($"Count: {myViewModel.Count}")
/// );
/// </code>
/// </example>
public sealed record StatePanelWidget(
    object StateKey,
    Func<StatePanelContext, Hex1bWidget> Builder) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        // 1. Identity resolution — find existing node by state key, not position
        var ancestorSP = context.FindAncestor<StatePanelNode>();
        StatePanelNode? node = null;

        if (ancestorSP != null)
        {
            // Registry lookup by reference identity
            ancestorSP.NestedStatePanels.TryGetValue(StateKey, out node);
        }
        else if (existingNode is StatePanelNode spn
                 && ReferenceEquals(spn.StateKey, StateKey))
        {
            // Standalone fallback: positional match with key check
            node = spn;
        }

        var isNew = node is null;
        node ??= new StatePanelNode();
        node.StateKey = StateKey;

        // 2. Register in ancestor's registry and mark as visited
        if (ancestorSP != null)
        {
            ancestorSP.NestedStatePanels[StateKey] = node;
            ancestorSP.MarkVisited(StateKey);
        }

        // 3. Advance animations by elapsed time since last frame
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (node.LastAdvanceTicks > 0 && node.Animations.HasActiveAnimations)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(node.LastAdvanceTicks, now);
            node.Animations.AdvanceAll(elapsed);
        }
        node.LastAdvanceTicks = now;

        // 4. Deferred builder — context can read current animation values
        var spContext = new StatePanelContext(node);
        var childWidget = Builder(spContext);

        // 5. Reconcile child subtree
        node.Child = await context.ReconcileChildAsync(node.Child, childWidget, node);

        // 6. Sweep nested state keys not visited this frame
        node.SweepUnvisited();

        // 7. Schedule re-render if animations are still running
        if (node.Animations.HasActiveAnimations && context.ScheduleTimerCallback is not null)
        {
            var capturedNode = node;
            var capturedInvalidate = context.InvalidateCallback;
            context.ScheduleTimerCallback(TimeSpan.FromMilliseconds(16), () =>
            {
                capturedNode.MarkDirty();
                capturedNode.Parent?.MarkDirty();
                capturedInvalidate?.Invoke();
            });
        }

        if (isNew)
            node.MarkDirty();

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(StatePanelNode);
}
