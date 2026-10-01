using Hex1b;
using Hex1b.Charts;

var sales = new[]
{
    new ChartItem("Jan", 42),
    new ChartItem("Feb", 58),
    new ChartItem("Mar", 35),
    new ChartItem("Apr", 71),
    new ChartItem("May", 49),
    new ChartItem("Jun", 63),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ColumnChart(sales)
        .Title("Monthly Sales")
        .ShowValues()
    )
    .Build();

await terminal.RunAsync();
