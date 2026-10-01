using Hex1b;
using Hex1b.Charts;

var expenses = new[]
{
    new ChartItem("Rent", 1200),
    new ChartItem("Food", 450),
    new ChartItem("Transport", 180),
    new ChartItem("Utilities", 120),
    new ChartItem("Entertainment", 200),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.DonutChart(expenses)
            .HoleSize(0)
            .Title("Monthly Expenses")
            .FillHeight(),
        v.Legend(expenses)
            .ShowValues()
            .ShowPercentages()
            .FormatValue(v => "$" + v.ToString("N0")),
    ]))
    .Build();

await terminal.RunAsync();
