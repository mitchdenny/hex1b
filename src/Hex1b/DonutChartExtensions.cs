using Hex1b.Charts;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="DonutChartWidget{T}"/> instances.
/// </summary>
public static class DonutChartExtensions
{
    /// <summary>
    /// Creates a donut chart bound to the specified data.
    /// </summary>
    public static DonutChartWidget<T> DonutChart<T>(this RootContext ctx, IReadOnlyList<T> data)
        => new() { Data = data };

    /// <summary>
    /// Creates a donut chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    public static DonutChartWidget<ChartItem> DonutChart(this RootContext ctx, IReadOnlyList<ChartItem> data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a donut chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    public static DonutChartWidget<ChartItem> DonutChart(this RootContext ctx, params ChartItem[] data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a donut chart bound to the specified data.
    /// </summary>
    public static DonutChartWidget<T> DonutChart<T, TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<T> data)
        where TParent : Hex1bWidget
        => new() { Data = data };

    /// <summary>
    /// Creates a donut chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    public static DonutChartWidget<ChartItem> DonutChart<TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<ChartItem> data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a donut chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    public static DonutChartWidget<ChartItem> DonutChart<TParent>(
        this WidgetContext<TParent> ctx, params ChartItem[] data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };
}
