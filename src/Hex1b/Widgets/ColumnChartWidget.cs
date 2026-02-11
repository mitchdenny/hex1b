using Hex1b.Charts;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a vertical column chart with support for simple, stacked, and grouped modes.
/// </summary>
/// <typeparam name="T">The type of data item bound to the chart.</typeparam>
/// <remarks>
/// <para>
/// ColumnChartWidget follows the generic data-binding pattern: provide your data and
/// selector functions to extract labels and values. For ad-hoc data, use the
/// <see cref="ChartItem"/> convenience type with pre-wired selectors.
/// </para>
/// <para>
/// Three data-binding approaches are supported:
/// <list type="bullet">
///   <item><c>.Value()</c> — Single series (simple mode)</item>
///   <item><c>.Series()</c> — Multiple named series from flat data (grouped/stacked)</item>
///   <item><c>.GroupBy()</c> — Pivot long-form data into series at runtime</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para>Simple chart with ad-hoc data:</para>
/// <code>
/// ctx.ColumnChart([new("Jan", 42), new("Feb", 58), new("Mar", 35)])
///     .ShowValues()
/// </code>
/// <para>Multi-series grouped chart:</para>
/// <code>
/// ctx.ColumnChart(sales)
///     .Label(s => s.Month)
///     .Series("Electronics", s => s.Electronics, Colors.Blue)
///     .Series("Clothing", s => s.Clothing, Colors.Red)
///     .Mode(ChartLayout.Grouped)
/// </code>
/// </example>
/// <seealso cref="BarChartWidget{T}"/>
/// <seealso cref="TimeSeriesChartWidget{T}"/>
/// <seealso cref="ScatterChartWidget{T}"/>
public sealed record ColumnChartWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the chart.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    /// <summary>
    /// Gets the function that extracts a category label from each data item.
    /// </summary>
    internal Func<T, string>? LabelSelector { get; init; }

    /// <summary>
    /// Gets the function that extracts the numeric value for single-series mode.
    /// </summary>
    internal Func<T, double>? ValueSelector { get; init; }

    /// <summary>
    /// Gets the named series definitions for multi-series mode.
    /// </summary>
    internal IReadOnlyList<ChartSeriesDef<T>>? SeriesDefs { get; init; }

    /// <summary>
    /// Gets the function that extracts a group key for pivoting long-form data into series.
    /// </summary>
    internal Func<T, string>? GroupBySelector { get; init; }

    /// <summary>
    /// Gets the chart display mode.
    /// </summary>
    internal ChartLayout ChartLayout { get; init; } = ChartLayout.Simple;

    /// <summary>
    /// Gets the explicit minimum value for the chart axis. When null, auto-derived from data.
    /// </summary>
    public double? Minimum { get; init; }

    /// <summary>
    /// Gets the explicit maximum value for the chart axis. When null, auto-derived from data.
    /// </summary>
    public double? Maximum { get; init; }

    /// <summary>
    /// Gets whether to display numeric values above each column.
    /// </summary>
    internal bool IsShowingValues { get; init; }

    /// <summary>
    /// Gets whether to display horizontal grid lines.
    /// </summary>
    internal bool IsShowingGridLines { get; init; }

    /// <summary>
    /// Gets the optional chart title displayed above the chart area.
    /// </summary>
    internal string? ChartTitle { get; init; }

    /// <summary>
    /// Gets the optional custom formatter for displaying numeric values.
    /// </summary>
    public Func<double, string>? ValueFormatter { get; init; }

    #region Fluent Methods

    /// <summary>
    /// Sets the function that extracts a category label from each data item.
    /// </summary>
    public ColumnChartWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <summary>
    /// Sets the function that extracts the numeric value for single-series mode.
    /// </summary>
    public ColumnChartWidget<T> Value(Func<T, double> selector)
        => this with { ValueSelector = selector };

    /// <summary>
    /// Adds a named series definition for multi-series mode (flat/wide data).
    /// </summary>
    /// <param name="name">The display name for this series.</param>
    /// <param name="selector">Function to extract the numeric value from each data item.</param>
    /// <param name="color">Optional color override for this series.</param>
    public ColumnChartWidget<T> Series(string name, Func<T, double> selector, Hex1bColor? color = null)
        => this with { SeriesDefs = [.. (SeriesDefs ?? []), new(name, selector, color)] };

    /// <summary>
    /// Sets the group-by selector for pivoting long-form data into series at runtime.
    /// </summary>
    public ColumnChartWidget<T> GroupBy(Func<T, string> groupSelector)
        => this with { GroupBySelector = groupSelector };

    /// <summary>
    /// Sets the explicit minimum value for the chart axis.
    /// </summary>
    public ColumnChartWidget<T> Min(double min) => this with { Minimum = min };

    /// <summary>
    /// Sets the explicit maximum value for the chart axis.
    /// </summary>
    public ColumnChartWidget<T> Max(double max) => this with { Maximum = max };

    /// <summary>
    /// Sets explicit minimum and maximum values for the chart axis.
    /// </summary>
    public ColumnChartWidget<T> Range(double min, double max)
        => this with { Minimum = min, Maximum = max };

    /// <summary>
    /// Sets whether to display numeric values above each column.
    /// </summary>
    public ColumnChartWidget<T> ShowValues(bool show = true)
        => this with { IsShowingValues = show };

    /// <summary>
    /// Sets whether to display horizontal grid lines.
    /// </summary>
    public ColumnChartWidget<T> ShowGridLines(bool show = true)
        => this with { IsShowingGridLines = show };

    /// <summary>
    /// Sets the chart title displayed above the chart area.
    /// </summary>
    public ColumnChartWidget<T> Title(string title)
        => this with { ChartTitle = title };

    /// <summary>
    /// Sets a custom formatter for displaying numeric values.
    /// </summary>
    public ColumnChartWidget<T> FormatValue(Func<double, string> formatter)
        => this with { ValueFormatter = formatter };

    /// <summary>
    /// Sets the chart display mode.
    /// </summary>
    public ColumnChartWidget<T> Layout(ChartLayout layout)
        => this with { ChartLayout = layout };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ColumnChartNode<T> ?? new ColumnChartNode<T>();

        node.MarkDirty();

        node.Data = Data;
        node.LabelSelector = LabelSelector;
        node.ValueSelector = ValueSelector;
        node.SeriesDefs = SeriesDefs;
        node.GroupBySelector = GroupBySelector;
        node.Mode = ChartLayout;
        node.Minimum = Minimum;
        node.Maximum = Maximum;
        node.ShowValues = IsShowingValues;
        node.ShowGridLines = IsShowingGridLines;
        node.Title = ChartTitle;
        node.ValueFormatter = ValueFormatter;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(ColumnChartNode<T>);
}
