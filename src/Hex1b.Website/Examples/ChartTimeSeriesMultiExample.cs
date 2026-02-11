using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Multi-Series Time Series Chart
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the timeSeriesMultiCode sample in:
/// src/content/guide/widgets/charts.md
/// </remarks>
public class ChartTimeSeriesMultiExample(ILogger<ChartTimeSeriesMultiExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartTimeSeriesMultiExample> _logger = logger;

    public override string Id => "chart-timeseries-multi";
    public override string Title => "Time Series - Multi-Series";
    public override string Description => "Demonstrates multi-series line chart with revenue vs expenses";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating time series multi-series example widget builder");

        var data = new[]
        {
            new FinRec("Jan", 150, 90), new FinRec("Feb", 130, 110),
            new FinRec("Mar", 105, 120), new FinRec("Apr", 95, 135),
            new FinRec("May", 120, 125), new FinRec("Jun", 160, 110),
            new FinRec("Jul", 175, 130), new FinRec("Aug", 140, 145),
            new FinRec("Sep", 110, 150), new FinRec("Oct", 130, 125),
            new FinRec("Nov", 170, 115), new FinRec("Dec", 190, 100),
        };

        var blue = Hex1bColor.FromRgb(66, 133, 244);
        var red = Hex1bColor.FromRgb(234, 67, 53);

        return () =>
        {
            var ctx = new RootContext();
            return ctx.TimeSeriesChart(data)
                .Label(d => d.Month)
                .Series("Revenue", d => d.Revenue, blue)
                .Series("Expenses", d => d.Expenses, red)
                .Title("Revenue vs Expenses")
                .ShowGridLines();
        };
    }

    private record FinRec(string Month, double Revenue, double Expenses);
}
