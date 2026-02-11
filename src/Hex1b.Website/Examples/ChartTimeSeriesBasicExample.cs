using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Basic Time Series Chart
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the timeSeriesBasicCode sample in:
/// src/content/guide/widgets/charts.md
/// </remarks>
public class ChartTimeSeriesBasicExample(ILogger<ChartTimeSeriesBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartTimeSeriesBasicExample> _logger = logger;

    public override string Id => "chart-timeseries-basic";
    public override string Title => "Time Series - Basic";
    public override string Description => "Demonstrates a basic time series line chart";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating time series basic example widget builder");

        var data = new[]
        {
            new ChartItem("Jan", 2), new ChartItem("Feb", 4),
            new ChartItem("Mar", 9), new ChartItem("Apr", 14),
            new ChartItem("May", 18), new ChartItem("Jun", 22),
            new ChartItem("Jul", 25), new ChartItem("Aug", 24),
            new ChartItem("Sep", 20), new ChartItem("Oct", 14),
            new ChartItem("Nov", 8), new ChartItem("Dec", 3),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.TimeSeriesChart(data)
                .Title("Monthly Temperature (°C)")
                .ShowGridLines();
        };
    }
}
