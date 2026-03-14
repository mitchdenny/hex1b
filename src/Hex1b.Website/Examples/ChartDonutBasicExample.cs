using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Donut Chart
/// Demonstrates a basic donut chart with proportional arc segments.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the donutBasicCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartDonutBasicExample(ILogger<ChartDonutBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartDonutBasicExample> _logger = logger;

    public override string Id => "chart-donut-basic";
    public override string Title => "Donut Chart - Basic Usage";
    public override string Description => "Demonstrates a donut chart with proportional arc segments";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating donut chart basic example widget builder");

        var languages = new[]
        {
            new ChartItem("Go", 42),
            new ChartItem("Rust", 28),
            new ChartItem("C#", 30),
            new ChartItem("Python", 55),
            new ChartItem("Java", 38),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.DonutChart(languages)
                    .Title("Language Popularity")
                    .FillHeight(),
                v.Legend(languages)
                    .ShowPercentages(),
            ]);
        };
    }
}
