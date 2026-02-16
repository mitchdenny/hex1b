using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for configuring per-widget render-cache hints.
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Adds a cache-eligibility predicate to this widget.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to configure.</param>
    /// <param name="predicate">
    /// Predicate evaluated against the reconciled node and current render context.
    /// Returning <c>false</c> forces
    /// a cache miss for that subtree on the current frame.
    /// </param>
    /// <returns>A new widget with the cache predicate set.</returns>
    public static TWidget Cached<TWidget>(this TWidget widget, Func<RenderCacheContext, bool> predicate)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { CachePredicate = predicate };
}
