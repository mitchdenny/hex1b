using Hex1b;
using Hex1b.Charts;

var data = new[]
{
    new ChartItem("00:00", 120), new ChartItem("04:00", 60),
    new ChartItem("08:00", 450), new ChartItem("12:00", 580),
    new ChartItem("16:00", 490), new ChartItem("20:00", 310),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.TimeSeriesChart(data)
        .Fill(FillStyle.Braille)
        .Title("Request Volume (24h)")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();
