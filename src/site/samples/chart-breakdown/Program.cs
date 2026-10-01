using Hex1b;
using Hex1b.Charts;

var diskUsage = new[]
{
    new ChartItem("Data", 42),
    new ChartItem("Packages", 18),
    new ChartItem("Temp", 9),
    new ChartItem("System", 15),
    new ChartItem("Other", 3),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.BreakdownChart(diskUsage)
            .Title("Disk Usage"),
        v.Legend(diskUsage)
            .ShowPercentages()
            .ShowValues(),
    ]))
    .Build();

await terminal.RunAsync();
