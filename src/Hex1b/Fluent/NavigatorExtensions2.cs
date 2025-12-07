namespace Hex1b.Fluent;

using System.Diagnostics.CodeAnalysis;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building NavigatorWidget.
/// </summary>
[Experimental("HEX1B001")]
public static class NavigatorExtensions2
{
    /// <summary>
    /// Creates a Navigator with the specified state.
    /// </summary>
    [Experimental("HEX1B001")]
    public static NavigatorWidget Navigator<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        NavigatorState navigatorState)
        where TParent : Hex1bWidget
        => new(navigatorState);

    /// <summary>
    /// Creates a Navigator with state selected from context state.
    /// </summary>
    [Experimental("HEX1B001")]
    public static NavigatorWidget Navigator<TParent, TState>(
        this WidgetCtx<TParent, TState> ctx,
        Func<TState, NavigatorState> stateSelector)
        where TParent : Hex1bWidget
        => new(stateSelector(ctx.State));
}
