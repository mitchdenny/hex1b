using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Grouped Scatter Chart
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the scatterGroupedCode sample in:
/// src/content/guide/widgets/charts.md
/// </remarks>
public class ChartScatterGroupedExample(ILogger<ChartScatterGroupedExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartScatterGroupedExample> _logger = logger;

    public override string Id => "chart-scatter-grouped";
    public override string Title => "Scatter Chart - Grouped";
    public override string Description => "Demonstrates a scatter plot with color-coded series groups";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scatter chart grouped example widget builder");

        var random = new Random(42);
        var data = Enumerable.Range(0, 90).Select(i =>
        {
            var group = i < 30 ? "Young" : i < 60 ? "Middle" : "Senior";
            var income = (group switch { "Young" => 30, "Middle" => 55, _ => 45 })
                + random.NextDouble() * 30;
            var spending = income * (0.5 + random.NextDouble() * 0.4);
            return new DemoPoint(income, spending, group);
        }).ToArray();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ScatterChart(data)
                .X(d => d.Income)
                .Y(d => d.Spending)
                .GroupBy(d => d.Group)
                .Title("Income vs Spending by Age Group")
                .ShowGridLines();
        };
    }

    private record DemoPoint(double Income, double Spending, string Group);
}
