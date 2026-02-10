using Hex1b.Theming;

namespace Hex1b.Charts;

/// <summary>
/// A convenience data type for ad-hoc chart data.
/// </summary>
/// <remarks>
/// <para>
/// When used with chart extension method overloads that accept <see cref="ChartItem"/>,
/// the <see cref="Label"/> and <see cref="Value"/> selectors are pre-wired automatically.
/// </para>
/// </remarks>
/// <param name="Label">The category label displayed on the chart axis.</param>
/// <param name="Value">The numeric value for this data point.</param>
/// <param name="Color">Optional color override. When null, the chart cycles through theme colors.</param>
public record ChartItem(string Label, double Value, Hex1bColor? Color = null);
