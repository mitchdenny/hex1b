using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

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

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.TimeSeriesChart(data)
        .Label(d => d.Month)
        .Series("Revenue", d => d.Revenue, blue)
        .Series("Expenses", d => d.Expenses, red)
        .Title("Revenue vs Expenses")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record FinRec(string Month, double Revenue, double Expenses);
