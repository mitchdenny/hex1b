namespace Hex1b;

using System.Diagnostics.CodeAnalysis;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building NavigatorWidget.
/// </summary>
[Experimental("HEX1B001")]
public static class NavigatorExtensions
{
    /// <summary>
    /// Creates a Navigator with the specified state.
    /// </summary>
    [Experimental("HEX1B001")]
    public static NavigatorWidget Navigator<TParent>(
        this WidgetContext<TParent> ctx,
        NavigatorState navigatorState)
        where TParent : Hex1bWidget
        => new(navigatorState);
}
