using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for assigning metric names to widgets.
/// </summary>
public static class MetricExtensions
{
    /// <summary>
    /// Sets the metric name for this widget. When per-node metrics are enabled,
    /// this name becomes a segment in the hierarchical metric path used as a tag
    /// value on per-node timing histograms (e.g., <c>hex1b.node.render.duration</c>).
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to name.</param>
    /// <param name="name">
    /// The metric name segment. Should be a short, descriptive identifier
    /// (e.g., <c>"sidebar"</c>, <c>"orders-table"</c>, <c>"editor"</c>).
    /// Ancestor names are automatically composed into a dot-separated path.
    /// </param>
    /// <returns>A copy of the widget with the metric name set.</returns>
    public static TWidget MetricName<TWidget>(this TWidget widget, string name)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { MetricName = name };
}
