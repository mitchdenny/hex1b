using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Multi-Series Column Chart
/// Demonstrates stacked, stacked 100%, and grouped column layouts.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the columnMultiSeriesCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartColumnMultiSeriesExample(ILogger<ChartColumnMultiSeriesExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartColumnMultiSeriesExample> _logger = logger;

    public override string Id => "chart-column-multiseries";
    public override string Title => "Column Chart - Multi-Series";
    public override string Description => "Demonstrates stacked column chart with multiple series";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating column chart multi-series example widget builder");

        var data = new[]
        {
            new SalesRecord("Jan", 50, 30, 20),
            new SalesRecord("Feb", 65, 40, 25),
            new SalesRecord("Mar", 45, 35, 30),
            new SalesRecord("Apr", 70, 50, 35),
        };

        var blue = Hex1bColor.FromRgb(66, 133, 244);
        var red = Hex1bColor.FromRgb(234, 67, 53);
        var green = Hex1bColor.FromRgb(52, 168, 83);

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ColumnChart(data)
                .Label(s => s.Month)
                .Series("Electronics", s => s.Electronics, blue)
                .Series("Clothing", s => s.Clothing, red)
                .Series("Food", s => s.Food, green)
                .Layout(ChartLayout.Stacked)
                .Title("Sales by Category")
                .ShowValues();
        };
    }

    private record SalesRecord(string Month, double Electronics, double Clothing, double Food);
}
