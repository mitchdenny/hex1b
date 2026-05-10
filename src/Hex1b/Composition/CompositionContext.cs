using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Composition;

/// <summary>
/// The hooks-style context passed to <see cref="Hex1bWidget.Build(CompositionContext)"/>.
/// Provides per-instance state storage and an ambient context API for sharing values
/// between a composite and its descendants.
/// </summary>
/// <remarks>
/// <para>
/// A new <see cref="CompositionContext"/> is constructed on every reconciliation pass, but
/// the underlying composite node persists, so calls to <see cref="UseState{T}(System.Func{T})"/>
/// return the same instance across frames.
/// </para>
/// <para>
/// Every value published with <see cref="Provide{T}(T)"/> lives on the composite's own node;
/// descendants resolve it by walking the ancestor chain. Use this for ambient values that
/// should flow naturally down a subtree (e.g. theme overrides, form context, controllers).
/// </para>
/// <para>
/// <see cref="CompositionContext"/> derives from <see cref="WidgetContext{TParentWidget}"/>,
/// so the full fluent widget-building API (<c>ctx.Text(...)</c>, <c>ctx.VStack(...)</c>,
/// <c>ctx.Button(...)</c>, etc.) is available directly inside <c>Build</c>.
/// </para>
/// </remarks>
public sealed class CompositionContext : WidgetContext<Hex1bWidget>
{
    private readonly Hex1bCompositeNode _node;
    private readonly ReconcileContext _reconcileContext;

    internal CompositionContext(Hex1bCompositeNode node, ReconcileContext reconcileContext)
    {
        _node = node;
        _reconcileContext = reconcileContext;
    }

    /// <summary>
    /// True the first time this composite is reconciled (i.e. its node was just created).
    /// Use this to perform one-time initialisation against state objects after they are
    /// created by <see cref="UseState{T}(System.Func{T})"/>.
    /// </summary>
    public bool IsNew => _reconcileContext.IsNew;

    /// <summary>
    /// The cancellation token associated with the current reconciliation pass. Composites
    /// that perform asynchronous work in <c>Build</c> overrides should observe this token.
    /// </summary>
    public CancellationToken CancellationToken => _reconcileContext.CancellationToken;

    /// <summary>
    /// Gets or creates a per-instance state object of type <typeparamref name="T"/>. The
    /// <paramref name="factory"/> is invoked once on first call; subsequent calls return
    /// the same instance for the lifetime of this composite node.
    /// </summary>
    /// <typeparam name="T">The state type. Must be a reference type.</typeparam>
    /// <param name="factory">Factory invoked on first access to create the state object.</param>
    /// <remarks>
    /// State is keyed by <typeparamref name="T"/>, so each composite may hold at most one
    /// instance of any given state type. To track multiple values of the same shape, wrap
    /// them in a single state class with named fields.
    /// </remarks>
    public T UseState<T>(Func<T> factory) where T : class
        => _node.GetState(factory);

    /// <summary>
    /// Publishes <paramref name="value"/> as ambient context for descendants of this
    /// composite. Any descendant <see cref="CompositionContext"/> within the same subtree
    /// can resolve it via <see cref="Use{T}"/> or <see cref="Require{T}"/>.
    /// </summary>
    /// <typeparam name="T">The ambient value type. Must be a reference type.</typeparam>
    /// <param name="value">The value to publish.</param>
    /// <remarks>
    /// Subsequent <c>Provide&lt;T&gt;</c> calls on the same composite replace the previous
    /// value. A nested composite that publishes a value of the same type shadows the outer
    /// value for its own subtree.
    /// </remarks>
    public void Provide<T>(T value) where T : class
        => _node.Provide(value);

    /// <summary>
    /// Resolves the nearest ambient value of type <typeparamref name="T"/> from an ancestor
    /// composite. Returns <c>null</c> if no ancestor has provided one.
    /// </summary>
    /// <typeparam name="T">The ambient value type. Must be a reference type.</typeparam>
    public T? Use<T>() where T : class
    {
        foreach (var node in _reconcileContext.EnumerateAncestors())
        {
            if (node is Hex1bCompositeNode composite && composite.TryGetProvided<T>(out var value))
                return value;
        }
        return null;
    }

    /// <summary>
    /// Resolves the nearest ambient value of type <typeparamref name="T"/> from an ancestor
    /// composite, throwing if none has been provided.
    /// </summary>
    /// <typeparam name="T">The ambient value type. Must be a reference type.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// No ancestor composite published a value of <typeparamref name="T"/>.
    /// </exception>
    public T Require<T>() where T : class
    {
        var value = Use<T>();
        if (value is null)
        {
            throw new InvalidOperationException(
                $"No ancestor composite has provided a value of type '{typeof(T).FullName}'. " +
                $"Call Provide<{typeof(T).Name}>() in an ancestor widget's Build method.");
        }
        return value;
    }
}

