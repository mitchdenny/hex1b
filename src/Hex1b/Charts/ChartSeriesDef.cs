using Hex1b.Theming;

namespace Hex1b.Charts;

/// <summary>
/// Defines a named data series extracted from chart data items.
/// </summary>
/// <remarks>
/// <para>
/// Each series definition specifies how to extract a numeric value from
/// data items of type <typeparamref name="T"/>, along with display metadata.
/// Built via the fluent <c>.Series()</c> method on chart widgets.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of data item the series extracts values from.</typeparam>
/// <param name="Name">The display name for this series (shown in legend).</param>
/// <param name="ValueSelector">Function to extract the numeric value from a data item.</param>
/// <param name="Color">Optional color override. When null, assigned from theme color cycling.</param>
public record ChartSeriesDef<T>(
    string Name,
    Func<T, double> ValueSelector,
    Hex1bColor? Color = null);
