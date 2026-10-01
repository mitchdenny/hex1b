using Hex1b;
using Hex1b.Charts;

var random = new Random(42);
var data = Enumerable.Range(0, 60).Select(_ =>
{
    var height = 150 + random.NextDouble() * 40;
    var weight = (height - 100) * 0.8 + random.NextDouble() * 20 - 10;
    return new Measurement(height, weight);
}).ToArray();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ScatterChart(data)
        .X(d => d.Height)
        .Y(d => d.Weight)
        .Title("Height vs Weight")
        .ShowGridLines()
    )
    .Build();

await terminal.RunAsync();

record Measurement(double Height, double Weight);
