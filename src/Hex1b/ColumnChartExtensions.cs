using Hex1b.Charts;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="ColumnChartWidget{T}"/> instances.
/// </summary>
public static class ColumnChartExtensions
{
    /// <summary>
    /// Creates a column chart bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <param name="ctx">The root context.</param>
    /// <param name="data">The data source for the chart.</param>
    public static ColumnChartWidget<T> ColumnChart<T>(this RootContext ctx, IReadOnlyList<T> data)
        => new() { Data = data };

    /// <summary>
    /// Creates a column chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    /// <param name="ctx">The root context.</param>
    /// <param name="data">The chart items.</param>
    public static ColumnChartWidget<ChartItem> ColumnChart(this RootContext ctx, IReadOnlyList<ChartItem> data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a column chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    /// <param name="ctx">The root context.</param>
    /// <param name="data">The chart items.</param>
    public static ColumnChartWidget<ChartItem> ColumnChart(this RootContext ctx, params ChartItem[] data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a column chart bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The data source for the chart.</param>
    public static ColumnChartWidget<T> ColumnChart<T, TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<T> data)
        where TParent : Hex1bWidget
        => new() { Data = data };

    /// <summary>
    /// Creates a column chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The chart items.</param>
    public static ColumnChartWidget<ChartItem> ColumnChart<TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<ChartItem> data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a column chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="data">The chart items.</param>
    public static ColumnChartWidget<ChartItem> ColumnChart<TParent>(
        this WidgetContext<TParent> ctx, params ChartItem[] data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };
}
