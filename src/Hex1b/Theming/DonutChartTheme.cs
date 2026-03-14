namespace Hex1b.Theming;

/// <summary>
/// Theme elements for DonutChart widgets.
/// </summary>
/// <remarks>
/// <para>
/// DonutChartTheme provides customization for the legend text colors used
/// alongside the donut chart visualization.
/// </para>
/// </remarks>
/// <example>
/// <para>Customize donut chart appearance:</para>
/// <code>
/// var theme = new Hex1bThemeBuilder()
///     .Set(DonutChartTheme.LegendLabelColor, Hex1bColor.White)
///     .Set(DonutChartTheme.LegendDimColor, Hex1bColor.Gray)
///     .Build();
/// </code>
/// </example>
/// <seealso cref="Widgets.DonutChartWidget{T}"/>
public static class DonutChartTheme
{
    /// <summary>
    /// The color used for legend labels.
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LegendLabelColor =
        new($"{nameof(DonutChartTheme)}.{nameof(LegendLabelColor)}", () => Hex1bColor.FromRgb(200, 200, 200));

    /// <summary>
    /// The color used for dim legend text (values, percentages).
    /// </summary>
    public static readonly Hex1bThemeElement<Hex1bColor> LegendDimColor =
        new($"{nameof(DonutChartTheme)}.{nameof(LegendDimColor)}", () => Hex1bColor.FromRgb(140, 140, 140));
}
