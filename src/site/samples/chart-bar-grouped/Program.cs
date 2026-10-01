using Hex1b;
using Hex1b.Charts;
using Hex1b.Theming;

var metrics = new[]
{
    new ServerMetric("web-01", 78.5, 62.3),
    new ServerMetric("web-02", 45.1, 38.7),
    new ServerMetric("db-01", 92.0, 85.4),
    new ServerMetric("cache", 23.8, 51.2),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.BarChart(metrics)
        .Label(m => m.Host)
        .Series("CPU %", m => m.Cpu, Hex1bColor.FromRgb(234, 67, 53))
        .Series("Memory %", m => m.Memory, Hex1bColor.FromRgb(66, 133, 244))
        .Layout(ChartLayout.Grouped)
        .Title("Server Resources")
        .Range(0, 100)
    )
    .Build();

await terminal.RunAsync();

record ServerMetric(string Host, double Cpu, double Memory);
