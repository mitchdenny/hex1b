using Hex1b;

var currentValue = 50.0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Volume Control"),
        v.Text(""),
        v.Text($"Current: {currentValue:F0}%"),
        v.Slider(50)
            .OnValueChanged(e => currentValue = e.Value),
        v.Text(""),
        v.Text("Use ← → or Home/End to adjust")
    ]))
    .Build();

await terminal.RunAsync();
