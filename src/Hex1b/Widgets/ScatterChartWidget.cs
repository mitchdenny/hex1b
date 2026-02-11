using Hex1b.Charts;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a scatter plot using braille characters for sub-cell precision.
/// </summary>
/// <typeparam name="T">The type of data item bound to the chart.</typeparam>
/// <remarks>
/// <para>
/// Points are NOT connected by lines. Each data point plots a single braille dot.
/// Multiple series can be created via <see cref="GroupBy"/> to color-code groups.
/// </para>
/// </remarks>
/// <example>
/// <para>Create a scatter chart with grouped series:</para>
/// <code>
/// var app = new Hex1bApp(ctx =&gt;
///     ctx.ScatterChart(data)
///         .X(d =&gt; d.Height)
///         .Y(d =&gt; d.Weight)
///         .GroupBy(d =&gt; d.Category)
///         .Title("Height vs Weight")
/// );
/// </code>
/// </example>
/// <seealso cref="TimeSeriesChartWidget{T}"/>
/// <seealso cref="ColumnChartWidget{T}"/>
public sealed record ScatterChartWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the chart.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    internal Func<T, double>? XSelector { get; init; }
    internal Func<T, double>? YSelector { get; init; }
    internal Func<T, string>? GroupBySelector { get; init; }
    internal string? ChartTitle { get; init; }
    internal bool IsShowingGridLines { get; init; } = true;
    internal double? XMin { get; init; }
    internal double? XMax { get; init; }
    internal double? YMin { get; init; }
    internal double? YMax { get; init; }
    internal Func<double, string>? XFormatter { get; init; }
    internal Func<double, string>? YFormatter { get; init; }

    #region Fluent Methods

    /// <summary>
    /// Sets the function that extracts the X-axis value from each data item.
    /// </summary>
    public ScatterChartWidget<T> X(Func<T, double> selector)
        => this with { XSelector = selector };

    /// <summary>
    /// Sets the function that extracts the Y-axis value from each data item.
    /// </summary>
    public ScatterChartWidget<T> Y(Func<T, double> selector)
        => this with { YSelector = selector };

    /// <summary>
    /// Sets the function that groups data into color-coded series.
    /// </summary>
    public ScatterChartWidget<T> GroupBy(Func<T, string> groupSelector)
        => this with { GroupBySelector = groupSelector };

    /// <summary>
    /// Sets the chart title.
    /// </summary>
    public ScatterChartWidget<T> Title(string title)
        => this with { ChartTitle = title };

    /// <summary>
    /// Sets whether to display grid lines.
    /// </summary>
    public ScatterChartWidget<T> ShowGridLines(bool show = true)
        => this with { IsShowingGridLines = show };

    /// <summary>
    /// Sets the explicit X-axis range.
    /// </summary>
    public ScatterChartWidget<T> XRange(double min, double max)
        => this with { XMin = min, XMax = max };

    /// <summary>
    /// Sets the explicit Y-axis range.
    /// </summary>
    public ScatterChartWidget<T> YRange(double min, double max)
        => this with { YMin = min, YMax = max };

    /// <summary>
    /// Sets a custom X value formatter.
    /// </summary>
    public ScatterChartWidget<T> FormatX(Func<double, string> formatter)
        => this with { XFormatter = formatter };

    /// <summary>
    /// Sets a custom Y value formatter.
    /// </summary>
    public ScatterChartWidget<T> FormatY(Func<double, string> formatter)
        => this with { YFormatter = formatter };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ScatterChartNode<T> ?? new ScatterChartNode<T>();
        node.MarkDirty();
        node.Data = Data;
        node.XSelector = XSelector;
        node.YSelector = YSelector;
        node.GroupBySelector = GroupBySelector;
        node.Title = ChartTitle;
        node.ShowGridLines = IsShowingGridLines;
        node.XMin = XMin;
        node.XMax = XMax;
        node.YMin = YMin;
        node.YMax = YMax;
        node.XFormatter = XFormatter;
        node.YFormatter = YFormatter;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(ScatterChartNode<T>);
}
