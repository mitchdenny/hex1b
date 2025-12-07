namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building HStack widgets.
/// </summary>
public static class HStackExtensions2
{
    /// <summary>
    /// Creates an HStack where the callback returns an array of children.
    /// </summary>
    public static HStackWidget HStack<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<WidgetCtx<HStackWidget, TState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<HStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new HStackWidget(children);
    }

    /// <summary>
    /// Creates an HStack with narrowed state.
    /// </summary>
    public static HStackWidget HStack<TParent, TState, TChildState>(
        this WidgetCtx<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetCtx<HStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<HStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new HStackWidget(children);
    }

    /// <summary>
    /// Creates an HStack with state selected from parent state.
    /// </summary>
    public static HStackWidget HStack<TParent, TState, TChildState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<TState, TChildState> stateSelector,
        Func<WidgetCtx<HStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<HStackWidget, TChildState>(stateSelector(ctx.State));
        var children = builder(childCtx);
        return new HStackWidget(children);
    }
}
