using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Standalone Legend
/// Demonstrates the Legend widget with various chart types and orientations.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the legendCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartLegendExample(ILogger<ChartLegendExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartLegendExample> _logger = logger;

    public override string Id => "chart-legend";
    public override string Title => "Legend Widget";
    public override string Description => "Demonstrates the standalone Legend widget with charts";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating legend example widget builder");

        var data = new[]
        {
            new ChartItem("Engineering", 2_450_000),
            new ChartItem("Marketing", 875_000),
            new ChartItem("Sales", 1_200_000),
            new ChartItem("Operations", 340_000),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.HStack(h => [
                    h.DonutChart(data)
                        .Title("Budget Allocation")
                        .FillHeight(),
                    h.Legend(data)
                        .ShowValues()
                        .ShowPercentages()
                        .FormatValue(v => "$" + (v / 1_000).ToString("N0") + "K"),
                ]),
                v.BreakdownChart(data)
                    .Title("Budget Breakdown"),
                v.Legend(data)
                    .Horizontal()
                    .ShowPercentages(),
            ]);
        };
    }
}
