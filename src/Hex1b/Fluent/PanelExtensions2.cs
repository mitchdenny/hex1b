namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building PanelWidget.
/// </summary>
public static class PanelExtensions2
{
    /// <summary>
    /// Creates a Panel wrapping a single child widget.
    /// </summary>
    public static PanelWidget Panel<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Creates a Panel with a VStack child.
    /// </summary>
    public static PanelWidget Panel<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<WidgetCtx<VStackWidget, TState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<VStackWidget, TState>(ctx.State);
        var children = builder(childCtx);
        return new PanelWidget(new VStackWidget(children));
    }

    /// <summary>
    /// Creates a Panel with narrowed state.
    /// </summary>
    public static PanelWidget Panel<TParent, TState, TChildState>(
        this WidgetCtx<TParent, TState> ctx,
        TChildState childState,
        Func<WidgetCtx<VStackWidget, TChildState>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetCtx<VStackWidget, TChildState>(childState);
        var children = builder(childCtx);
        return new PanelWidget(new VStackWidget(children));
    }
}
