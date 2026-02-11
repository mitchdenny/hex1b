using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Basic Bar Chart
/// Demonstrates a simple horizontal bar chart using ChartItem convenience data.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the barBasicCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartBarBasicExample(ILogger<ChartBarBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartBarBasicExample> _logger = logger;

    public override string Id => "chart-bar-basic";
    public override string Title => "Bar Chart - Basic Usage";
    public override string Description => "Demonstrates a simple horizontal bar chart with ad-hoc ChartItem data";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating bar chart basic example widget builder");

        var sales = new[]
        {
            new ChartItem("Jan", 42),
            new ChartItem("Feb", 58),
            new ChartItem("Mar", 35),
            new ChartItem("Apr", 71),
            new ChartItem("May", 49),
            new ChartItem("Jun", 63),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.BarChart(sales)
                .Title("Monthly Sales")
                .ShowValues();
        };
    }
}
