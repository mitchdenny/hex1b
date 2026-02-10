using Hex1b.Charts;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a horizontal bar chart with support for simple, stacked, and grouped modes.
/// </summary>
/// <typeparam name="T">The type of data item bound to the chart.</typeparam>
/// <remarks>
/// <para>
/// BarChartWidget follows the same generic data-binding pattern as <see cref="ColumnChartWidget{T}"/>,
/// but renders horizontal bars growing left-to-right.
/// </para>
/// </remarks>
/// <example>
/// <para>Simple chart with ad-hoc data:</para>
/// <code>
/// ctx.BarChart([new("Alpha", 8), new("Beta", 5), new("Gamma", 2)])
///     .ShowValues()
/// </code>
/// </example>
public sealed record BarChartWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the chart.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.LabelSelector"/>
    internal Func<T, string>? LabelSelector { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ValueSelector"/>
    internal Func<T, double>? ValueSelector { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.SeriesDefs"/>
    internal IReadOnlyList<ChartSeriesDef<T>>? SeriesDefs { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.GroupBySelector"/>
    internal Func<T, string>? GroupBySelector { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ChartLayout"/>
    internal ChartLayout ChartLayout { get; init; } = ChartLayout.Simple;

    /// <inheritdoc cref="ColumnChartWidget{T}.Minimum"/>
    public double? Minimum { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.Maximum"/>
    public double? Maximum { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ShowValues"/>
    internal bool IsShowingValues { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ShowGridLines"/>
    internal bool IsShowingGridLines { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.Title"/>
    internal string? ChartTitle { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ValueFormatter"/>
    public Func<double, string>? ValueFormatter { get; init; }

    #region Fluent Methods

    /// <inheritdoc cref="ColumnChartWidget{T}.Label"/>
    public BarChartWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <inheritdoc cref="ColumnChartWidget{T}.Value"/>
    public BarChartWidget<T> Value(Func<T, double> selector)
        => this with { ValueSelector = selector };

    /// <inheritdoc cref="ColumnChartWidget{T}.Series"/>
    public BarChartWidget<T> Series(string name, Func<T, double> selector, Hex1bColor? color = null)
        => this with { SeriesDefs = [.. (SeriesDefs ?? []), new(name, selector, color)] };

    /// <inheritdoc cref="ColumnChartWidget{T}.GroupBy"/>
    public BarChartWidget<T> GroupBy(Func<T, string> groupSelector)
        => this with { GroupBySelector = groupSelector };

    /// <inheritdoc cref="ColumnChartWidget{T}.Min"/>
    public BarChartWidget<T> Min(double min) => this with { Minimum = min };

    /// <inheritdoc cref="ColumnChartWidget{T}.Max"/>
    public BarChartWidget<T> Max(double max) => this with { Maximum = max };

    /// <inheritdoc cref="ColumnChartWidget{T}.Range"/>
    public BarChartWidget<T> Range(double min, double max)
        => this with { Minimum = min, Maximum = max };

    /// <inheritdoc cref="ColumnChartWidget{T}.ShowValues"/>
    public BarChartWidget<T> ShowValues(bool show = true)
        => this with { IsShowingValues = show };

    /// <inheritdoc cref="ColumnChartWidget{T}.ShowGridLines"/>
    public BarChartWidget<T> ShowGridLines(bool show = true)
        => this with { IsShowingGridLines = show };

    /// <inheritdoc cref="ColumnChartWidget{T}.Title"/>
    public BarChartWidget<T> Title(string title)
        => this with { ChartTitle = title };

    /// <inheritdoc cref="ColumnChartWidget{T}.FormatValue"/>
    public BarChartWidget<T> FormatValue(Func<double, string> formatter)
        => this with { ValueFormatter = formatter };

    /// <inheritdoc cref="ColumnChartWidget{T}.Layout"/>
    public BarChartWidget<T> Layout(ChartLayout layout)
        => this with { ChartLayout = layout };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BarChartNode<T> ?? new BarChartNode<T>();

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

    internal override Type GetExpectedNodeType() => typeof(BarChartNode<T>);
}
