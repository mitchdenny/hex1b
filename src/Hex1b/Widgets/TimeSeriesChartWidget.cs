using Hex1b.Charts;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a time series line chart using braille characters for sub-cell precision.
/// </summary>
/// <typeparam name="T">The type of data item bound to the chart.</typeparam>
/// <remarks>
/// <para>
/// Points are connected by lines rendered with braille dots (2×4 dots per cell).
/// Multiple series are rendered on separate layers and composited with OR'd braille patterns.
/// </para>
/// </remarks>
public sealed record TimeSeriesChartWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the chart.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    internal Func<T, string>? LabelSelector { get; init; }
    internal Func<T, double>? ValueSelector { get; init; }
    internal IReadOnlyList<ChartSeriesDef<T>>? SeriesDefs { get; init; }
    internal string? ChartTitle { get; init; }
    internal bool IsShowingValues { get; init; }
    internal bool IsShowingGridLines { get; init; } = true;
    internal FillStyle ChartFillStyle { get; init; } = FillStyle.None;
    internal double? Minimum { get; init; }
    internal double? Maximum { get; init; }
    internal Func<double, string>? ValueFormatter { get; init; }

    #region Fluent Methods

    /// <summary>
    /// Sets the function that extracts an X-axis label from each data item.
    /// </summary>
    public TimeSeriesChartWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <summary>
    /// Sets the function that extracts the Y value for single-series mode.
    /// </summary>
    public TimeSeriesChartWidget<T> Value(Func<T, double> selector)
        => this with { ValueSelector = selector };

    /// <summary>
    /// Adds a named series definition for multi-series mode.
    /// </summary>
    public TimeSeriesChartWidget<T> Series(string name, Func<T, double> selector, Hex1bColor? color = null)
        => this with { SeriesDefs = [.. (SeriesDefs ?? []), new(name, selector, color)] };

    /// <summary>
    /// Sets the chart title.
    /// </summary>
    public TimeSeriesChartWidget<T> Title(string title)
        => this with { ChartTitle = title };

    /// <summary>
    /// Sets whether to display Y values at data points.
    /// </summary>
    public TimeSeriesChartWidget<T> ShowValues(bool show = true)
        => this with { IsShowingValues = show };

    /// <summary>
    /// Sets whether to display grid lines.
    /// </summary>
    public TimeSeriesChartWidget<T> ShowGridLines(bool show = true)
        => this with { IsShowingGridLines = show };

    /// <summary>
    /// Sets the area fill style below the line.
    /// </summary>
    public TimeSeriesChartWidget<T> Fill(FillStyle style = FillStyle.Solid)
        => this with { ChartFillStyle = style };

    /// <summary>
    /// Sets the explicit minimum Y value.
    /// </summary>
    public TimeSeriesChartWidget<T> Min(double min) => this with { Minimum = min };

    /// <summary>
    /// Sets the explicit maximum Y value.
    /// </summary>
    public TimeSeriesChartWidget<T> Max(double max) => this with { Maximum = max };

    /// <summary>
    /// Sets explicit Y-axis range.
    /// </summary>
    public TimeSeriesChartWidget<T> Range(double min, double max)
        => this with { Minimum = min, Maximum = max };

    /// <summary>
    /// Sets a custom Y value formatter.
    /// </summary>
    public TimeSeriesChartWidget<T> FormatValue(Func<double, string> formatter)
        => this with { ValueFormatter = formatter };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TimeSeriesChartNode<T> ?? new TimeSeriesChartNode<T>();
        node.MarkDirty();
        node.Data = Data;
        node.LabelSelector = LabelSelector;
        node.ValueSelector = ValueSelector;
        node.SeriesDefs = SeriesDefs;
        node.Title = ChartTitle;
        node.ShowValues = IsShowingValues;
        node.ShowGridLines = IsShowingGridLines;
        node.FillStyle = ChartFillStyle;
        node.Minimum = Minimum;
        node.Maximum = Maximum;
        node.ValueFormatter = ValueFormatter;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TimeSeriesChartNode<T>);
}
