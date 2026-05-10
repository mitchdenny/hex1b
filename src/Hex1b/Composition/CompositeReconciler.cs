using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Composition;

/// <summary>
/// Implements the default compositional reconciliation pipeline used by
/// <see cref="Hex1bWidget.ReconcileAsync(Hex1bNode?, ReconcileContext)"/>.
/// </summary>
/// <remarks>
/// <para>
/// All composite widgets reconcile into a shared <see cref="Hex1bCompositeNode"/>.
/// The reconciler:
/// </para>
/// <list type="number">
/// <item><description>recycles or creates the composite node;</description></item>
/// <item><description>disposes prior state if the node is being repurposed by a different widget type;</description></item>
/// <item><description>snapshots descendant focus, invokes <c>Build</c>, then reconciles the returned tree as the node's child;</description></item>
/// <item><description>restores focus inside the subtree if the rebuild orphaned it.</description></item>
/// </list>
/// </remarks>
internal static class CompositeReconciler
{
    public static async Task<Hex1bNode> ReconcileAsync(
        Hex1bWidget widget,
        Hex1bNode? existingNode,
        ReconcileContext context)
    {
        var node = existingNode as Hex1bCompositeNode;

        // If this node was previously owned by a different widget type, dispose its
        // state and start over so we don't leak references between unrelated composites.
        if (node is not null && node.CompositeWidgetType != widget.GetType())
        {
            node.DisposeAllState();
            node = null;
        }

        var isNew = node is null;
        node ??= new Hex1bCompositeNode();
        node.CompositeWidgetType = widget.GetType();

        // Snapshot focus state for the composite's subtree BEFORE rebuild.
        // If a descendant currently owns focus and rebuild discards that node
        // (e.g. because a child shifted position in a VStack), focus would be
        // lost — and FocusRing.EnsureFocus would snap focus to the FIRST
        // focusable in the entire app, which usually isn't what the composite
        // author wanted. We restore focus to a focusable inside this subtree
        // after the rebuild so focus stays "with" the composite.
        var previouslyFocusedDescendant = FindFocusedDescendant(node);
        var previousFocusedWidgetType = previouslyFocusedDescendant?.GetType();

        var compositionContext = new CompositionContext(node, context);
        var built = widget.InvokeBuild(compositionContext);
        if (built is null)
        {
            throw new InvalidOperationException(
                $"Widget '{widget.GetType().FullName}' does not override Build or ReconcileAsync. " +
                "Compositional widgets must override Build and return a non-null widget tree; " +
                "primitive widgets must override ReconcileAsync and GetExpectedNodeType.");
        }

        node.Child = await context.ReconcileChildAsync(node.Child, built, node);

        if (isNew)
            node.MarkDirty();

        // Restore focus inside the composite's subtree if rebuild orphaned it.
        if (previouslyFocusedDescendant is not null)
        {
            var currentFocused = FindFocusedDescendant(node);
            if (currentFocused is null)
            {
                // Prefer a focusable of the same node type as the previously
                // focused one (e.g. a TextBoxNode that shifted position). Fall
                // back to the first focusable in the subtree.
                var focusables = node.GetFocusableNodes().ToList();
                var preferred = previousFocusedWidgetType is not null
                    ? focusables.FirstOrDefault(n => n.GetType() == previousFocusedWidgetType)
                    : null;
                var target = preferred ?? focusables.FirstOrDefault();
                if (target is not null)
                {
                    ReconcileContext.SetNodeFocus(target, true);
                }
            }
        }

        return node;
    }

    private static Hex1bNode? FindFocusedDescendant(Hex1bCompositeNode node)
    {
        foreach (var focusable in node.GetFocusableNodes())
        {
            if (focusable.IsFocused)
                return focusable;
        }
        return null;
    }
}
