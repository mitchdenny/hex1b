using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Custom Value Formatting
/// Demonstrates FormatValue() on ColumnChart and BreakdownChart with currency formatting.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the formattingCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartFormattingExample(ILogger<ChartFormattingExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartFormattingExample> _logger = logger;

    public override string Id => "chart-formatting";
    public override string Title => "Charts - Custom Value Formatting";
    public override string Description => "Demonstrates custom value formatting on charts";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating chart formatting example widget builder");

        var budgets = new[]
        {
            new Department("Engineering", 2_450_000),
            new Department("Marketing", 875_000),
            new Department("Sales", 1_200_000),
            new Department("Operations", 340_000),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.ColumnChart(budgets)
                    .Label(d => d.Name)
                    .Value(d => d.Budget)
                    .Title("Department Budgets")
                    .ShowValues()
                    .FormatValue(v => "$" + (v / 1_000_000).ToString("F1") + "M"),
                v.BreakdownChart(budgets)
                    .Label(d => d.Name)
                    .Value(d => d.Budget)
                    .Title("Budget Allocation")
                    .ShowValues()
                    .ShowPercentages()
                    .FormatValue(v => "$" + (v / 1_000).ToString("N0") + "K"),
            ]);
        };
    }

    private record Department(string Name, double Budget);
}
