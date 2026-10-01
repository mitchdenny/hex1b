using Hex1b;
using Hex1b.Automation;
using Hex1b.Layout;
using Hex1b.Widgets;

if (args.Length != 1) throw new ArgumentException("Usage: StaticPreviews <output-directory>");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);

await GenerateAsync("qrcode-quietzone", 50, 18, context => context.VStack(v => [
    v.Text("Without quiet zone:"),
    v.QrCode("https://example.com").QuietZone(0)
]));
await GenerateAsync("slider-custom-range", 50, 4, context => context.VStack(v => [
    v.Text("Temperature (-10°C to 40°C)"),
    v.Slider(initialValue: 22, min: -10, max: 40)
]));
await GenerateAsync("slider-step", 50, 4, context => context.VStack(v => [
    v.Text("Volume (steps of 10)"),
    v.Slider(initialValue: 50, min: 0, max: 100, step: 10)
]));

async Task GenerateAsync(string name, int width, int height, Func<RootContext, Hex1bWidget> builder)
{
    const string marker = "static-preview-ready";
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHeadless().WithDimensions(width, height + 1)
        .WithHex1bApp(context => context.VStack(v => [
            builder(context).FixedHeight(height), v.Text(marker)
        ])).Build();
    var run = terminal.RunAsync(stop.Token);
    try
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(screen => screen.ContainsText(marker), TimeSpan.FromSeconds(5), name)
            .Build().ApplyAsync(terminal, stop.Token);
        var region = terminal.CreateSnapshot().GetRegion(new Rect(0, 0, width, height));
        var svg = region.ToSvg(new TerminalSvgOptions
        {
            ShowCellGrid = false,
            ShowPixelGrid = false,
            DefaultBackground = "#0f0f1a",
            DefaultForeground = "#e0e0e0",
            FontFamily = "'Cascadia Code', Consolas, monospace",
            FontSize = 14,
            CellWidth = 9,
            CellHeight = 18
        });
        await File.WriteAllTextAsync(Path.Combine(output, $"{name}.svg"), svg, stop.Token);
        Console.WriteLine($"Generated {name}.svg");
    }
    finally
    {
        stop.Cancel();
        try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
}
