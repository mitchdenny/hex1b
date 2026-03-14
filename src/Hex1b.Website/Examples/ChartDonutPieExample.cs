using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Pie Chart (Donut with HoleSize 0)
/// Demonstrates a solid pie chart using DonutChart with HoleSize(0).
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the donutPieCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartDonutPieExample(ILogger<ChartDonutPieExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartDonutPieExample> _logger = logger;

    public override string Id => "chart-donut-pie";
    public override string Title => "Pie Chart";
    public override string Description => "Demonstrates a solid pie chart using DonutChart with HoleSize(0)";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating pie chart example widget builder");

        var expenses = new[]
        {
            new ChartItem("Rent", 1200),
            new ChartItem("Food", 450),
            new ChartItem("Transport", 180),
            new ChartItem("Utilities", 120),
            new ChartItem("Entertainment", 200),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.DonutChart(expenses)
                    .HoleSize(0)
                    .Title("Monthly Expenses")
                    .FillHeight(),
                v.Legend(expenses)
                    .ShowValues()
                    .ShowPercentages()
                    .FormatValue(v => "$" + v.ToString("N0")),
            ]);
        };
    }
}
