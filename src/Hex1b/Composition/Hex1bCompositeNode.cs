using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// The single node type that backs every <see cref="Composition.Hex1bCompositeWidget"/>.
/// Acts as a layout-invisible identity anchor whose layout, arrangement, rendering, and
/// focus are all delegated to its single built child.
/// </summary>
/// <remarks>
/// <para>
/// All composite widgets reconcile into a <see cref="Hex1bCompositeNode"/> regardless of
/// their concrete widget type. The node tracks which composite widget type produced it via
/// <see cref="CompositeWidgetType"/>; if that type changes between frames, the composite
/// reconciler disposes prior state and starts fresh.
/// </para>
/// <para>
/// Per-instance state is stored in a typed dictionary and surfaced to user code through
/// <see cref="Composition.CompositionContext.UseState{T}(System.Func{T})"/>. Ambient values
/// published with <see cref="Composition.CompositionContext.Provide{T}(T)"/> are stored in a
/// separate dictionary on this node and looked up by descendants walking the ancestor chain.
/// </para>
/// </remarks>
public sealed class Hex1bCompositeNode : Hex1bNode
{
    private readonly Dictionary<Type, object> _stateStore = new();
    private Dictionary<Type, object>? _provides;

    /// <summary>
    /// The runtime <see cref="Type"/> of the composite widget that last reconciled this node.
    /// Used to detect when an existing node is being repurposed by a different composite type
    /// so that previously stored state can be safely disposed.
    /// </summary>
    internal Type? CompositeWidgetType { get; set; }

    /// <summary>
    /// The single child node produced by the composite's <c>Build</c> method.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    internal T GetState<T>(Func<T> factory) where T : class
    {
        if (_stateStore.TryGetValue(typeof(T), out var existing) && existing is T typed)
            return typed;

        var state = factory();
        _stateStore[typeof(T)] = state;
        return state;
    }

    internal void Provide<T>(T value) where T : class
    {
        _provides ??= new Dictionary<Type, object>();
        _provides[typeof(T)] = value;
    }

    internal bool TryGetProvided<T>(out T value) where T : class
    {
        if (_provides is not null && _provides.TryGetValue(typeof(T), out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Disposes any stored state objects that implement <see cref="IDisposable"/> and
    /// clears both the state and provides stores. Called when this node is being repurposed
    /// by a different composite type or torn down entirely.
    /// </summary>
    internal void DisposeAllState()
    {
        foreach (var state in _stateStore.Values)
        {
            if (state is IDisposable disposable)
                disposable.Dispose();
        }
        _stateStore.Clear();
        _provides?.Clear();
    }

    // --- Layout pass-through (mirrors StatePanelNode) ---

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Child is null)
            return constraints.Constrain(Size.Zero);
        return Child.Measure(constraints);
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
            context.RenderChild(Child);
    }

    // --- Focus pass-through ---

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
        => Child != null ? new[] { Child } : Array.Empty<Hex1bNode>();
}
