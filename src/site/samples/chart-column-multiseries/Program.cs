using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

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

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ColumnChart(data)
        .Label(s => s.Month)
        .Series("Electronics", s => s.Electronics, blue)
        .Series("Clothing", s => s.Clothing, red)
        .Series("Food", s => s.Food, green)
        .Layout(ChartLayout.Stacked)
        .Title("Sales by Category")
        .ShowValues()
    )
    .Build();

await terminal.RunAsync();

record SalesRecord(string Month, double Electronics, double Clothing, double Food);
