using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A standalone widget that displays a chart legend with colored swatches and labels.
/// </summary>
/// <typeparam name="T">The type of data item bound to the legend.</typeparam>
/// <remarks>
/// <para>
/// LegendWidget renders a list of labeled color swatches, optionally showing values
/// and/or percentages. It can be placed anywhere in the widget tree independently
/// of chart widgets.
/// </para>
/// <para>
/// Supports both vertical (one item per row) and horizontal (all items on one row)
/// orientation via <see cref="Horizontal"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.DonutChart(data).FillHeight()
/// ctx.Legend(data).ShowPercentages().Horizontal()
/// </code>
/// </example>
public sealed record LegendWidget<T> : Hex1bWidget
{
    /// <summary>
    /// Gets the data source for the legend.
    /// </summary>
    public IReadOnlyList<T>? Data { get; init; }

    /// <summary>
    /// Gets the function that extracts a label from each data item.
    /// </summary>
    internal Func<T, string>? LabelSelector { get; init; }

    /// <summary>
    /// Gets the function that extracts the numeric value from each data item.
    /// </summary>
    internal Func<T, double>? ValueSelector { get; init; }

    /// <summary>
    /// Gets whether to display absolute values alongside labels.
    /// </summary>
    internal bool IsShowingValues { get; init; }

    /// <summary>
    /// Gets whether to display percentages alongside labels.
    /// </summary>
    internal bool IsShowingPercentages { get; init; }

    /// <summary>
    /// Gets whether to render items horizontally on a single row.
    /// When false, renders vertically with one item per row (default).
    /// </summary>
    internal bool IsHorizontal { get; init; }

    /// <summary>
    /// Gets the custom value formatter for legend display.
    /// When <see langword="null"/>, the default chart formatter is used.
    /// </summary>
    public Func<double, string>? ValueFormatter { get; init; }

    #region Fluent Methods

    /// <summary>
    /// Sets the function that extracts a label from each data item.
    /// </summary>
    public LegendWidget<T> Label(Func<T, string> selector)
        => this with { LabelSelector = selector };

    /// <summary>
    /// Sets the function that extracts the numeric value from each data item.
    /// </summary>
    public LegendWidget<T> Value(Func<T, double> selector)
        => this with { ValueSelector = selector };

    /// <summary>
    /// Sets whether to display absolute values in the legend.
    /// </summary>
    public LegendWidget<T> ShowValues(bool show = true)
        => this with { IsShowingValues = show };

    /// <summary>
    /// Sets whether to display percentages in the legend.
    /// </summary>
    public LegendWidget<T> ShowPercentages(bool show = true)
        => this with { IsShowingPercentages = show };

    /// <summary>
    /// Sets the legend to render items horizontally on a single row.
    /// </summary>
    public LegendWidget<T> Horizontal(bool horizontal = true)
        => this with { IsHorizontal = horizontal };

    /// <summary>
    /// Sets a custom value formatter for legend display.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Legend(data)
    ///     .ShowValues()
    ///     .FormatValue(v =&gt; $"${v:F2}")
    /// </code>
    /// </example>
    public LegendWidget<T> FormatValue(Func<double, string> formatter)
        => this with { ValueFormatter = formatter };

    #endregion

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as LegendNode<T> ?? new LegendNode<T>();

        node.MarkDirty();

        node.Data = Data;
        node.LabelSelector = LabelSelector;
        node.ValueSelector = ValueSelector;
        node.ShowValues = IsShowingValues;
        node.ShowPercentages = IsShowingPercentages;
        node.IsHorizontal = IsHorizontal;
        node.ValueFormatter = ValueFormatter;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(LegendNode<T>);
}
