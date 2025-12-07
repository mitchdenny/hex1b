namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building BorderWidget.
/// </summary>
public static class BorderExtensions2
{
    /// <summary>
    /// Creates a Border wrapping a single child widget.
    /// </summary>
    public static BorderWidget Border<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Hex1bWidget child,
        string? title = null)
        where TParent : Hex1bWidget
        => new(child, title);

    /// <summary>
    /// Creates a Border with a VStack child.
    /// </summary>
    public static BorderWidget Border<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<WidgetCtx<VStackWidget, TState>, Hex1bWidget[]> builder,
        string? title = null)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<VStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new BorderWidget(new VStackWidget(children), title);
    }

    /// <summary>
    /// Creates a Border with narrowed state.
    /// </summary>
    public static BorderWidget Border<TParent, TState, TChildState>(
        this WidgetCtx<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetCtx<VStackWidget, TChildState>, Hex1bWidget[]> builder,
        string? title = null)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<VStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new BorderWidget(new VStackWidget(children), title);
    }
}
