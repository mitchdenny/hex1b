using Hex1b.Charts;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a proportional segmented bar with an optional legend.
/// </summary>
/// <typeparam name="T">The type of data item bound to the chart.</typeparam>
/// <remarks>
/// <para>
/// BreakdownChartWidget always shows proportional data (100% of the total).
/// Each segment's width is proportional to its value relative to the sum of all values.
/// </para>
/// <para>
/// Unlike <see cref="ColumnChartWidget{T}"/> and <see cref="BarChartWidget{T}"/>,
/// this widget only supports a single series — no grouped or stacked modes.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.BreakdownChart([new("Data", 42), new("Packages", 18), new("Temp", 9)])
///     .ShowPercentages()
///     .Title("Disk Usage")
/// </code>
/// </example>
/// <seealso cref="ColumnChartWidget{T}"/>
/// <seealso cref="BarChartWidget{T}"/>
public sealed record BreakdownChartWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the chart.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    /// <summary>
    /// Gets the function that extracts a segment label from each data item.
    /// </summary>
    internal Func<T, string>? LabelSelector { get; init; }

    /// <summary>
    /// Gets the function that extracts the numeric value from each data item.
    /// </summary>
    internal Func<T, double>? ValueSelector { get; init; }

    /// <summary>
    /// Gets whether to display absolute values in the legend.
    /// </summary>
    internal bool IsShowingValues { get; init; }

    /// <summary>
    /// Gets whether to display percentages in the legend.
    /// </summary>
    internal bool IsShowingPercentages { get; init; }

    /// <summary>
    /// Gets the optional chart title displayed above the bar.
    /// </summary>
    internal string? ChartTitle { get; init; }

    #region Fluent Methods

    /// <summary>
    /// Sets the function that extracts a segment label from each data item.
    /// </summary>
    public BreakdownChartWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <summary>
    /// Sets the function that extracts the numeric value from each data item.
    /// </summary>
    public BreakdownChartWidget<T> Value(Func<T, double> selector)
        => this with { ValueSelector = selector };

    /// <summary>
    /// Sets whether to display absolute values in the legend.
    /// </summary>
    public BreakdownChartWidget<T> ShowValues(bool show = true)
        => this with { IsShowingValues = show };

    /// <summary>
    /// Sets whether to display percentages in the legend.
    /// </summary>
    public BreakdownChartWidget<T> ShowPercentages(bool show = true)
        => this with { IsShowingPercentages = show };

    /// <summary>
    /// Sets the chart title displayed above the bar.
    /// </summary>
    public BreakdownChartWidget<T> Title(string title)
        => this with { ChartTitle = title };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BreakdownChartNode<T> ?? new BreakdownChartNode<T>();

        node.MarkDirty();

        node.Data = Data;
        node.LabelSelector = LabelSelector;
        node.ValueSelector = ValueSelector;
        node.ShowValues = IsShowingValues;
        node.ShowPercentages = IsShowingPercentages;
        node.Title = ChartTitle;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(BreakdownChartNode<T>);
}
