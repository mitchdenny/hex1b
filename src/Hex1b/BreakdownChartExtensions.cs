using Hex1b.Charts;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="BreakdownChartWidget{T}"/> instances.
/// </summary>
public static class BreakdownChartExtensions
{
    /// <summary>
    /// Creates a breakdown chart bound to the specified data.
    /// </summary>
    public static BreakdownChartWidget<T> BreakdownChart<T>(this RootContext ctx, IReadOnlyList<T> data)
        => new() { Data = data };

    /// <summary>
    /// Creates a breakdown chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    public static BreakdownChartWidget<ChartItem> BreakdownChart(this RootContext ctx, IReadOnlyList<ChartItem> data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a breakdown chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    public static BreakdownChartWidget<ChartItem> BreakdownChart(this RootContext ctx, params ChartItem[] data)
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a breakdown chart bound to the specified data.
    /// </summary>
    public static BreakdownChartWidget<T> BreakdownChart<T, TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<T> data)
        where TParent : Hex1bWidget
        => new() { Data = data };

    /// <summary>
    /// Creates a breakdown chart with <see cref="ChartItem"/> data (selectors pre-wired).
    /// </summary>
    public static BreakdownChartWidget<ChartItem> BreakdownChart<TParent>(
        this WidgetContext<TParent> ctx, IReadOnlyList<ChartItem> data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };

    /// <summary>
    /// Creates a breakdown chart with <see cref="ChartItem"/> data (params overload).
    /// </summary>
    public static BreakdownChartWidget<ChartItem> BreakdownChart<TParent>(
        this WidgetContext<TParent> ctx, params ChartItem[] data)
        where TParent : Hex1bWidget
        => new() { Data = data, LabelSelector = i => i.Label, ValueSelector = i => i.Value };
}
