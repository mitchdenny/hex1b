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

    /// <inheritdoc cref="ColumnChartWidget{T}.Mode"/>
    public ChartMode Mode { get; init; } = ChartMode.Simple;

    /// <inheritdoc cref="ColumnChartWidget{T}.Minimum"/>
    public double? Minimum { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.Maximum"/>
    public double? Maximum { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ShowValues"/>
    public bool ShowValues { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.ShowGridLines"/>
    public bool ShowGridLines { get; init; }

    /// <inheritdoc cref="ColumnChartWidget{T}.Title"/>
    public string? Title { get; init; }

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

    /// <inheritdoc cref="ColumnChartWidget{T}.WithShowValues"/>
    public BarChartWidget<T> WithShowValues(bool show = true)
        => this with { ShowValues = show };

    /// <inheritdoc cref="ColumnChartWidget{T}.WithShowGridLines"/>
    public BarChartWidget<T> WithShowGridLines(bool show = true)
        => this with { ShowGridLines = show };

    /// <inheritdoc cref="ColumnChartWidget{T}.WithTitle"/>
    public BarChartWidget<T> WithTitle(string title)
        => this with { Title = title };

    /// <inheritdoc cref="ColumnChartWidget{T}.FormatValue"/>
    public BarChartWidget<T> FormatValue(Func<double, string> formatter)
        => this with { ValueFormatter = formatter };

    /// <inheritdoc cref="ColumnChartWidget{T}.WithMode"/>
    public BarChartWidget<T> WithMode(ChartMode mode)
        => this with { Mode = mode };

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
        node.Mode = Mode;
        node.Minimum = Minimum;
        node.Maximum = Maximum;
        node.ShowValues = ShowValues;
        node.ShowGridLines = ShowGridLines;
        node.Title = Title;
        node.ValueFormatter = ValueFormatter;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(BarChartNode<T>);
}
