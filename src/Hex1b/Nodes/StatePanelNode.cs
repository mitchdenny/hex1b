using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// A layout-invisible identity anchor node. Uses a state object's reference identity
/// as its lookup key, enabling identity-based reconciliation instead of positional.
/// Layout, arrangement, rendering, and focus are all passed through to the single child.
/// </summary>
/// <remarks>
/// <para>
/// StatePanelNode provides generic state storage via <see cref="GetState{T}"/>. Subsystems
/// (e.g. animation) layer on top by storing their own state objects, without StatePanelNode
/// knowing about them directly.
/// </para>
/// </remarks>
public sealed class StatePanelNode : Hex1bNode
{
    private readonly Dictionary<Type, object> _stateStore = new();

    /// <summary>
    /// The state object whose reference identity anchors this node.
    /// </summary>
    public object StateKey { get; set; } = null!;

    /// <summary>
    /// The single child node.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Stopwatch ticks at the last reconciliation. Used to compute elapsed time
    /// between reconciliation frames.
    /// </summary>
    internal long LastReconcileTicks { get; set; }

    /// <summary>
    /// Gets or creates a state object of the specified type. The factory is called once
    /// on first access; subsequent calls return the same instance.
    /// </summary>
    /// <typeparam name="T">The state type. Must be a reference type.</typeparam>
    /// <param name="factory">Factory invoked on first access to create the state object.</param>
    internal T GetState<T>(Func<T> factory) where T : class
    {
        if (_stateStore.TryGetValue(typeof(T), out var existing) && existing is T typed)
            return typed;

        var state = factory();
        _stateStore[typeof(T)] = state;
        return state;
    }

    /// <summary>
    /// Disposes all stored state objects that implement <see cref="IDisposable"/>
    /// and clears the state store.
    /// </summary>
    internal void DisposeAllState()
    {
        foreach (var state in _stateStore.Values)
        {
            if (state is IDisposable disposable)
                disposable.Dispose();
        }
        _stateStore.Clear();
    }

    /// <summary>
    /// Returns true if any stored state implements <see cref="IActiveState"/> and is active.
    /// </summary>
    internal bool HasActiveState
    {
        get
        {
            foreach (var state in _stateStore.Values)
            {
                if (state is IActiveState active && active.IsActive)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Advances all stored state that implements <see cref="IActiveState"/> by the given
    /// elapsed time. Called once per reconciliation frame before the builder runs.
    /// </summary>
    internal void AdvanceActiveState(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return;
        foreach (var state in _stateStore.Values)
        {
            if (state is IActiveState active)
                active.OnFrameAdvance(elapsed);
        }
    }

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
                kvp.Value.DisposeAllState();
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
