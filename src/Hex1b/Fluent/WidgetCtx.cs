namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Marker record for the root context (no parent widget constraint).
/// </summary>
public sealed record RootWidget : Hex1bWidget;

/// <summary>
/// A context for building widgets within a parent container.
/// The TParentWidget type constrains which child widgets can be created.
/// Extension methods return widgets directly; covariance allows collection expressions.
/// </summary>
/// <typeparam name="TParentWidget">The parent widget type - constrains valid children.</typeparam>
/// <typeparam name="TState">The state type available in this context.</typeparam>
public class WidgetCtx<TParentWidget, TState>
    where TParentWidget : Hex1bWidget
{
    /// <summary>
    /// The state available in this context.
    /// </summary>
    public TState State { get; }

    public WidgetCtx(TState state)
    {
        State = state;
    }

    /// <summary>
    /// Narrow the state to a child state, preserving the parent widget constraint.
    /// </summary>
    public WidgetCtx<TParentWidget, TChildState> With<TChildState>(Func<TState, TChildState> selector)
        => new(selector(State));

    /// <summary>
    /// Narrow the state to a child state, preserving the parent widget constraint.
    /// </summary>
    public WidgetCtx<TParentWidget, TChildState> With<TChildState>(TChildState childState)
        => new(childState);
}

/// <summary>
/// Root context for starting widget tree construction.
/// </summary>
public class RootCtx<TState> : WidgetCtx<RootWidget, TState>
{
    public RootCtx(TState state) : base(state) { }
}
