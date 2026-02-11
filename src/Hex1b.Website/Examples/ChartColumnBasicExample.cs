using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Basic Column Chart
/// Demonstrates a simple column chart using ChartItem convenience data.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the columnBasicCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartColumnBasicExample(ILogger<ChartColumnBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartColumnBasicExample> _logger = logger;

    public override string Id => "chart-column-basic";
    public override string Title => "Column Chart - Basic Usage";
    public override string Description => "Demonstrates a simple column chart with ad-hoc ChartItem data";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating column chart basic example widget builder");

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
            return ctx.ColumnChart(sales)
                .Title("Monthly Sales")
                .ShowValues();
        };
    }
}
