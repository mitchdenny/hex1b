using Hex1b.Charts;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="TimeSeriesChartWidget{T}"/> instances.
/// </summary>
public static class TimeSeriesChartExtensions
{
    /// <summary>
    /// Creates a time series chart bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <param name="context">The root context.</param>
    /// <param name="data">The data source for the chart.</param>
    public static TimeSeriesChartWidget<T> TimeSeriesChart<T>(this RootContext context, IReadOnlyList<T> data)
        => new() { Data = data };

    /// <summary>
    /// Creates a time series chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    /// <param name="context">The root context.</param>
    /// <param name="data">The chart items.</param>
    public static TimeSeriesChartWidget<ChartItem> TimeSeriesChart(this RootContext context, IReadOnlyList<ChartItem> data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a time series chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    /// <param name="context">The root context.</param>
    /// <param name="data">The chart items.</param>
    public static TimeSeriesChartWidget<ChartItem> TimeSeriesChart(this RootContext context, params ChartItem[] data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a time series chart bound to the specified data.
    /// </summary>
    /// <typeparam name="T">The data item type.</typeparam>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="data">The data source for the chart.</param>
    public static TimeSeriesChartWidget<T> TimeSeriesChart<T, TParent>(
        this WidgetContext<TParent> context, IReadOnlyList<T> data)
        where TParent : Hex1bWidget
        => new() { Data = data };

    /// <summary>
    /// Creates a time series chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="data">The chart items.</param>
    public static TimeSeriesChartWidget<ChartItem> TimeSeriesChart<TParent>(
        this WidgetContext<TParent> context, IReadOnlyList<ChartItem> data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a time series chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="data">The chart items.</param>
    public static TimeSeriesChartWidget<ChartItem> TimeSeriesChart<TParent>(
        this WidgetContext<TParent> context, params ChartItem[] data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };
}
