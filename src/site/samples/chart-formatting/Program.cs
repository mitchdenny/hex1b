using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var budgets = new[]
{
    new Department("Engineering", 2_450_000),
    new Department("Marketing", 875_000),
    new Department("Sales", 1_200_000),
    new Department("Operations", 340_000),
};

var blue = Hex1bColor.FromRgb(66, 133, 244);
var red = Hex1bColor.FromRgb(234, 67, 53);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.ColumnChart(budgets)
            .Label(d => d.Name)
            .Value(d => d.Budget)
            .Title("Department Budgets")
            .ShowValues()
            .FormatValue(v => "$" + (v / 1_000_000).ToString("F1") + "M"),
        v.BreakdownChart(budgets)
            .Label(d => d.Name)
            .Value(d => d.Budget)
            .Title("Budget Allocation"),
        v.Legend(budgets)
            .Label(d => d.Name)
            .Value(d => d.Budget)
            .ShowValues()
            .ShowPercentages()
            .FormatValue(v => "$" + (v / 1_000).ToString("N0") + "K"),
    ]))
    .Build();

await terminal.RunAsync();

record Department(string Name, double Budget);
