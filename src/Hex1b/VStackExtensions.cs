namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building VStack widgets.
/// The callback returns Hex1bWidget[] using collection expressions.
/// Covariance on Hex1bWidget allows mixing different widget types.
/// </summary>
public static class VStackExtensions
{
    /// <summary>
    /// Creates a VStack where the callback returns an array of children.
    /// Use collection expression syntax: v => [v.Text("a"), v.Button("b", () => {})]
    /// </summary>
    public static VStackWidget VStack<TParent, TState>(
        this WidgetContext<TParent, TState> ctx,
        Func<WidgetContext<VStackWidget, TState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new VStackWidget(children);
    }

    /// <summary>
    /// Creates a VStack with narrowed state.
    /// First argument is the child state, enabling progressive state narrowing.
    /// </summary>
    public static VStackWidget VStack<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetContext<VStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new VStackWidget(children);
    }

    /// <summary>
    /// Creates a VStack with state selected from parent state.
    /// </summary>
    public static VStackWidget VStack<TParent, TState, TChildState>(
        this WidgetContext<TParent, TState> ctx,
        Func<TState, TChildState> stateSelector,
        Func<WidgetContext<VStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget, TChildState>(stateSelector(ctx.State));
        var children = builder(childCtx);
        return new VStackWidget(children);
    }
}
