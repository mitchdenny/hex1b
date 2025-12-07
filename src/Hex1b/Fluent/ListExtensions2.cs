namespace Hex1b.Fluent;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ListWidget.
/// </summary>
public static class ListExtensions2
{
    /// <summary>
    /// Creates a List with the specified state.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        ListState listState)
        where TParent : Hex1bWidget
        => new(listState);

    /// <summary>
    /// Creates a List with state selected from context state.
    /// </summary>
    public static ListWidget List<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<TState, ListState> stateSelector)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State));
}
