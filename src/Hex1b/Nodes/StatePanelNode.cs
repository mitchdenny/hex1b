using Hex1b.Animation;
using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// A layout-invisible identity anchor node. Uses a state object's reference identity
/// as its lookup key, enabling identity-based reconciliation instead of positional.
/// Layout, arrangement, rendering, and focus are all passed through to the single child.
/// </summary>
public sealed class StatePanelNode : Hex1bNode
{
    /// <summary>
    /// The state object whose reference identity anchors this node.
    /// </summary>
    public object StateKey { get; set; } = null!;

    /// <summary>
    /// The single child node.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The animation collection for this identity scope.
    /// Animations persist across reconciliation frames for the same state key.
    /// </summary>
    public AnimationCollection Animations { get; internal set; } = new();

    /// <summary>
    /// Stopwatch ticks at the last animation advance. Used to compute elapsed time
    /// between reconciliation frames for animation advancement.
    /// </summary>
    internal long LastAdvanceTicks { get; set; }

    /// <summary>
    /// Registry for nested StatePanels, keyed by state object reference identity.
    /// During reconciliation, nested StatePanelWidgets look up their state key here
    /// instead of relying on positional matching.
    /// </summary>
    internal Dictionary<object, StatePanelNode> NestedStatePanels { get; }
        = new(ReferenceEqualityComparer.Instance);

    private readonly HashSet<object> _visitedKeys = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Marks a nested state key as visited during the current reconciliation frame.
    /// </summary>
    internal void MarkVisited(object key) => _visitedKeys.Add(key);

    /// <summary>
    /// Removes nested StatePanelNodes whose state keys were not visited this frame.
    /// Called after child subtree reconciliation is complete.
    /// </summary>
    internal void SweepUnvisited()
    {
        var toRemove = new List<object>();
        foreach (var kvp in NestedStatePanels)
        {
            if (!_visitedKeys.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                kvp.Value.Animations.DisposeAll();
            }
        }
        foreach (var key in toRemove)
            NestedStatePanels.Remove(key);
        _visitedKeys.Clear();
    }

    // --- Layout pass-through ---

    public override Size Measure(Constraints constraints)
    {
        if (Child is null)
            return constraints.Constrain(Size.Zero);
        return Child.Measure(constraints);
    }

    public override void Arrange(Rect rect)
    {
        base.Arrange(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
            context.RenderChild(Child);
    }

    // --- Focus pass-through (like PaddingNode) ---

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    public override IReadOnlyList<Hex1bNode> GetChildren()
    {
        return Child != null ? [Child] : [];
    }
}
