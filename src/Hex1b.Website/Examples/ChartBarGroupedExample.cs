using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Generic Data Binding
/// Demonstrates binding a custom data type with grouped bar layout.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the genericBindingCode sample in:
/// src/content/guide/widgets/charts.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ChartBarGroupedExample(ILogger<ChartBarGroupedExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartBarGroupedExample> _logger = logger;

    public override string Id => "chart-bar-grouped";
    public override string Title => "Bar Chart - Grouped with Generic Binding";
    public override string Description => "Demonstrates grouped bar chart with custom data type binding";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating bar chart grouped example widget builder");

        var metrics = new[]
        {
            new ServerMetric("web-01", 78.5, 62.3),
            new ServerMetric("web-02", 45.1, 38.7),
            new ServerMetric("db-01", 92.0, 85.4),
            new ServerMetric("cache", 23.8, 51.2),
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.BarChart(metrics)
                .Label(m => m.Host)
                .Series("CPU %", m => m.Cpu, Hex1bColor.FromRgb(234, 67, 53))
                .Series("Memory %", m => m.Memory, Hex1bColor.FromRgb(66, 133, 244))
                .Layout(ChartLayout.Grouped)
                .Title("Server Resources")
                .Range(0, 100);
        };
    }

    private record ServerMetric(string Host, double Cpu, double Memory);
}
