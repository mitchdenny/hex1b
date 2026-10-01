using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var data = new[]
{
    new RegionSales("Jan", 40, 25, 15), new RegionSales("Feb", 45, 30, 20),
    new RegionSales("Mar", 38, 35, 25), new RegionSales("Apr", 55, 32, 18),
    new RegionSales("May", 48, 40, 30), new RegionSales("Jun", 60, 38, 22),
    new RegionSales("Jul", 52, 45, 28), new RegionSales("Aug", 58, 42, 35),
    new RegionSales("Sep", 50, 48, 30), new RegionSales("Oct", 65, 40, 25),
    new RegionSales("Nov", 55, 50, 32), new RegionSales("Dec", 70, 45, 28),
};

var blue = Hex1bColor.FromRgb(66, 133, 244);
var red = Hex1bColor.FromRgb(234, 67, 53);
var green = Hex1bColor.FromRgb(52, 168, 83);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.TimeSeriesChart(data)
        .Label(d => d.Month)
        .Series("North", d => d.North, blue)
        .Series("South", d => d.South, red)
        .Series("West", d => d.West, green)
        .Layout(ChartLayout.Stacked)
        .Fill(FillStyle.Braille)
        .Title("Regional Sales (Stacked)")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record RegionSales(string Month, double North, double South, double West);
