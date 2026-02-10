using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Time Series with Area Fill
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the timeSeriesFillCode sample in:
/// src/content/guide/widgets/charts.md
/// </remarks>
public class ChartTimeSeriesFillExample(ILogger<ChartTimeSeriesFillExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartTimeSeriesFillExample> _logger = logger;

    public override string Id => "chart-timeseries-fill";
    public override string Title => "Time Series - Area Fill";
    public override string Description => "Demonstrates a time series chart with braille area fill";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating time series fill example widget builder");

        var data = new[]
        {
            new ChartItem("00:00", 120), new ChartItem("04:00", 60),
            new ChartItem("08:00", 450), new ChartItem("12:00", 580),
            new ChartItem("16:00", 490), new ChartItem("20:00", 310),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.TimeSeriesChart(data)
                .Fill(FillStyle.Braille)
                .Title("Request Volume (24h)")
                .ShowGridLines();
        };
    }
}
