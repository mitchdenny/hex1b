using Hex1b.Charts;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="ScatterChartWidget{T}"/> instances.
/// </summary>
public static class ScatterChartExtensions
{
    /// <summary>
    /// Creates a scatter chart bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <param name="ctx">The root context.</param>
    /// <param name="data">The data source for the chart.</param>
    public static ScatterChartWidget<T> ScatterChart<T>(this RootContext ctx, IReadOnlyList<T> data)
        => new() { Data = data };

    /// <summary>
    /// Creates a scatter chart bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The data source for the chart.</param>
    public static ScatterChartWidget<T> ScatterChart<T, TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<T> data)
        where TParent : Hex1bWidget
        => new() { Data = data };
}
