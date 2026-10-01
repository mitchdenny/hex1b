using Hex1b;
using Hex1b.Charts;

var data = new[]
{
    new ChartItem("Engineering", 2_450_000),
    new ChartItem("Marketing", 875_000),
    new ChartItem("Sales", 1_200_000),
    new ChartItem("Operations", 340_000),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.HStack(h => [
            h.DonutChart(data)
                .Title("Budget Allocation")
                .FillHeight(),
            h.Legend(data)
                .ShowValues()
                .ShowPercentages()
                .FormatValue(v => "$" + (v / 1_000).ToString("N0") + "K"),
        ]),
        v.BreakdownChart(data)
            .Title("Budget Breakdown"),
        v.Legend(data)
            .Horizontal()
            .ShowPercentages(),
    ]))
    .Build();

await terminal.RunAsync();
