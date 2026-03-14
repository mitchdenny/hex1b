using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that displays a donut (or pie) chart using half-block characters for smooth rendering.
/// </summary>
/// <typeparam name="T">The type of data item bound to the chart.</typeparam>
/// <remarks>
/// <para>
/// DonutChartWidget renders proportional data as colored arc segments around a ring.
/// Each segment's arc length is proportional to its value relative to the sum of all values.
/// </para>
/// <para>
/// The chart uses Unicode half-block characters (▀/▄) with independent foreground and
/// background colors to achieve 2× vertical resolution, producing smooth circular shapes
/// in the terminal.
/// </para>
/// <para>
/// Set <see cref="HoleSizeRatio"/> to <c>0.0</c> for a solid pie chart or leave at the
/// default <c>0.5</c> for a classic donut.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.DonutChart([new("Go", 42), new("Rust", 28), new("C#", 30)])
///     .Title("Languages")
///     .ShowPercentages()
/// </code>
/// </example>
/// <seealso cref="BreakdownChartWidget{T}"/>
public sealed record DonutChartWidget<T> : Hex1bWidget
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
    /// Gets the optional chart title displayed above the donut.
    /// </summary>
    internal string? ChartTitle { get; init; }

    /// <summary>
    /// Gets the inner radius as a fraction of the outer radius (0.0 = solid pie, 1.0 = thin ring).
    /// Default is 0.5.
    /// </summary>
    internal double HoleSizeRatio { get; init; } = 0.5;

    /// <summary>
    /// Gets the custom value formatter for legend display.
    /// When <see langword="null"/>, the default chart formatter is used.
    /// </summary>
    public Func<double, string>? ValueFormatter { get; init; }

    #region Fluent Methods

    /// <summary>
    /// Sets the function that extracts a segment label from each data item.
    /// </summary>
    public DonutChartWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <summary>
    /// Sets the function that extracts the numeric value from each data item.
    /// </summary>
    public DonutChartWidget<T> Value(Func<T, double> selector)
        => this with { ValueSelector = selector };

    /// <summary>
    /// Sets whether to display absolute values in the legend.
    /// </summary>
    public DonutChartWidget<T> ShowValues(bool show = true)
        => this with { IsShowingValues = show };

    /// <summary>
    /// Sets whether to display percentages in the legend.
    /// </summary>
    public DonutChartWidget<T> ShowPercentages(bool show = true)
        => this with { IsShowingPercentages = show };

    /// <summary>
    /// Sets the chart title displayed above the donut.
    /// </summary>
    public DonutChartWidget<T> Title(string title)
        => this with { ChartTitle = title };

    /// <summary>
    /// Sets the inner radius as a fraction of the outer radius.
    /// </summary>
    /// <param name="ratio">
    /// A value between 0.0 (solid pie chart) and 1.0 (thin ring). Default is 0.5.
    /// </param>
    public DonutChartWidget<T> HoleSize(double ratio)
        => this with { HoleSizeRatio = Math.Clamp(ratio, 0.0, 0.95) };

    /// <summary>
    /// Sets a custom value formatter for legend display.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.DonutChart(data)
    ///     .ShowValues()
    ///     .FormatValue(v =&gt; $"${v:F2}")
    /// </code>
    /// </example>
    public DonutChartWidget<T> FormatValue(Func<double, string> formatter)
        => this with { ValueFormatter = formatter };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DonutChartNode<T> ?? new DonutChartNode<T>();

        node.MarkDirty();

        node.Data = Data;
        node.LabelSelector = LabelSelector;
        node.ValueSelector = ValueSelector;
        node.ShowValues = IsShowingValues;
        node.ShowPercentages = IsShowingPercentages;
        node.Title = ChartTitle;
        node.HoleSizeRatio = HoleSizeRatio;
        node.ValueFormatter = ValueFormatter;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(DonutChartNode<T>);
}
