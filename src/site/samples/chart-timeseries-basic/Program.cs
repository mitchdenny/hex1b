using Hex1b;
using Hex1b.Charts;

var data = new[]
{
    new ChartItem("Jan", 2), new ChartItem("Feb", 4),
    new ChartItem("Mar", 9), new ChartItem("Apr", 14),
    new ChartItem("May", 18), new ChartItem("Jun", 22),
    new ChartItem("Jul", 25), new ChartItem("Aug", 24),
    new ChartItem("Sep", 20), new ChartItem("Oct", 14),
    new ChartItem("Nov", 8), new ChartItem("Dec", 3),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.TimeSeriesChart(data)
        .Title("Monthly Temperature (°C)")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();
