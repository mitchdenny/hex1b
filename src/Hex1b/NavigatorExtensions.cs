namespace Hex1b;

using System.Diagnostics.CodeAnalysis;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building <see cref="NavigatorWidget"/> using the fluent context API.
/// </summary>
/// <remarks>
/// <para>
/// These extensions add the <c>Navigator()</c> method to the widget context, allowing you to
/// create navigator widgets using the fluent API pattern.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var navState = new NavigatorState(rootRoute);
/// 
/// // Use via fluent context
/// var widget = ctx.VStack(v => [
///     v.Navigator(navState).FillHeight(),
///     v.InfoBar(sections)
/// ]);
/// </code>
/// </example>
[Experimental("HEX1B001")]
public static class NavigatorExtensions
{
    /// <summary>
    /// Creates a <see cref="NavigatorWidget"/> with the specified state.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the fluent context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="navigatorState">
    /// The <see cref="NavigatorState"/> that manages the navigation stack.
    /// Create one instance and keep a reference to it for calling navigation methods.
    /// </param>
    /// <returns>A new NavigatorWidget instance.</returns>
    /// <remarks>
    /// <para>
    /// The navigator widget manages a stack of routes and handles transitions between them.
    /// Pass a NavigatorState instance to control navigation from your event handlers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var nav = new NavigatorState(
    ///     new NavigatorRoute("home", n => BuildHomeScreen(n))
    /// );
    /// 
    /// var widget = ctx.VStack(v => [
    ///     v.Navigator(nav).FillHeight()
    /// ]);
    /// </code>
    /// </example>
    [Experimental("HEX1B001")]
    public static NavigatorWidget Navigator<TParent>(
        this WidgetContext<TParent> ctx,
        NavigatorState navigatorState)
        where TParent : Hex1bWidget
        => new(navigatorState);
}
