using Hex1b;
using Hex1b.Charts;

var languages = new[]
{
    new ChartItem("Go", 42),
    new ChartItem("Rust", 28),
    new ChartItem("C#", 30),
    new ChartItem("Python", 55),
    new ChartItem("Java", 38),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.DonutChart(languages)
            .Title("Language Popularity")
            .FillHeight(),
        v.Legend(languages)
            .ShowPercentages(),
    ]))
    .Build();

await terminal.RunAsync();
