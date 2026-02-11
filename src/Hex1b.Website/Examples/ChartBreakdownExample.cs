using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Breakdown Chart
/// Demonstrates a proportional segmented bar with percentages and values.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the breakdownCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartBreakdownExample(ILogger<ChartBreakdownExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartBreakdownExample> _logger = logger;

    public override string Id => "chart-breakdown";
    public override string Title => "Breakdown Chart";
    public override string Description => "Demonstrates a proportional segmented bar with legend";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating breakdown chart example widget builder");

        var diskUsage = new[]
        {
            new ChartItem("Data", 42),
            new ChartItem("Packages", 18),
            new ChartItem("Temp", 9),
            new ChartItem("System", 15),
            new ChartItem("Other", 3),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.BreakdownChart(diskUsage)
                .Title("Disk Usage")
                .ShowPercentages()
                .ShowValues();
        };
    }
}
