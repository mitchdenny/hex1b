namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building SplitterWidget.
/// </summary>
public static class SplitterExtensions2
{
    /// <summary>
    /// Creates a Splitter with left and right child widgets.
    /// </summary>
    public static SplitterWidget Splitter<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Hex1bWidget left,
        Hex1bWidget right,
        int leftWidth = 30)
        where TParent : Hex1bWidget
        => new(left, right, leftWidth);

    /// <summary>
    /// Creates a Splitter where both panes are VStacks built from callbacks.
    /// </summary>
    public static SplitterWidget Splitter<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<WidgetCtx<VStackWidget, TState>, Hex1bWidget[]> leftBuilder,
        Func<WidgetCtx<VStackWidget, TState>, Hex1bWidget[]> rightBuilder,
        int leftWidth = 30)
        where TParent : Hex1bWidget
    {
        var leftCtx = new WidgetCtx<VStackWidget, TState>(ctx.State);
        var rightCtx = new WidgetCtx<VStackWidget, TState>(ctx.State);
        return new SplitterWidget(
            new VStackWidget(leftBuilder(leftCtx)),
            new VStackWidget(rightBuilder(rightCtx)),
            leftWidth);
    }

    /// <summary>
    /// Creates a Splitter with narrowed state for child panes.
    /// </summary>
    public static SplitterWidget Splitter<TParent, TState, TLeftState, TRightState>(
        this WidgetCtx<TParent, TState> ctx,
        TLeftState leftState,
        Func<WidgetCtx<VStackWidget, TLeftState>, Hex1bWidget[]> leftBuilder,
        TRightState rightState,
        Func<WidgetCtx<VStackWidget, TRightState>, Hex1bWidget[]> rightBuilder,
        int leftWidth = 30)
        where TParent : Hex1bWidget
    {
        var leftCtx = new WidgetCtx<VStackWidget, TLeftState>(leftState);
        var rightCtx = new WidgetCtx<VStackWidget, TRightState>(rightState);
        return new SplitterWidget(
            new VStackWidget(leftBuilder(leftCtx)),
            new VStackWidget(rightBuilder(rightCtx)),
            leftWidth);
    }
}
