using Hex1b;
using Hex1b.Charts;

var random = new Random(42);
var data = Enumerable.Range(0, 90).Select(i =>
{
    var group = i < 30 ? "Young" : i < 60 ? "Middle" : "Senior";
    var income = (group switch { "Young" => 30, "Middle" => 55, _ => 45 })
        + random.NextDouble() * 30;
    var spending = income * (0.5 + random.NextDouble() * 0.4);
    return new DemoPoint(income, spending, group);
}).ToArray();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ScatterChart(data)
        .X(d => d.Income)
        .Y(d => d.Spending)
        .GroupBy(d => d.Group)
        .Title("Income vs Spending by Age Group")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record DemoPoint(double Income, double Spending, string Group);
