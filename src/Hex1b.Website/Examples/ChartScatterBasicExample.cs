using Hex1b;
using Hex1b.Charts;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Chart Widget Documentation: Basic Scatter Chart
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the scatterBasicCode sample in:
/// src/content/guide/widgets/charts.md
/// </remarks>
public class ChartScatterBasicExample(ILogger<ChartScatterBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ChartScatterBasicExample> _logger = logger;

    public override string Id => "chart-scatter-basic";
    public override string Title => "Scatter Chart - Basic";
    public override string Description => "Demonstrates a basic scatter plot with braille dot plotting";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scatter chart basic example widget builder");

        var random = new Random(42);
        var data = Enumerable.Range(0, 60).Select(_ =>
        {
            var height = 150 + random.NextDouble() * 40;
            var weight = (height - 100) * 0.8 + random.NextDouble() * 20 - 10;
            return new Measurement(height, weight);
        }).ToArray();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ScatterChart(data)
                .X(d => d.Height)
                .Y(d => d.Weight)
                .Title("Height vs Weight")
                .ShowGridLines();
        };
    }

    private record Measurement(double Height, double Weight);
}
