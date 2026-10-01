using Hex1b;

var state = new OverlayState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("═══════════════════════════════════════"),
        v.Text("         Main Application Area         "),
        v.Text("═══════════════════════════════════════"),
        v.Text(""),
        v.Text("  Content goes here..."),
        // Float overlay with score and controls
        v.Float(v.Text($"Score: {state.Score}")).Absolute(2, 0),
        v.Float(v.Button("+1 Point").OnClick(_ => state.Score++)).Absolute(2, 8),
        v.Float(v.Button("Reset").OnClick(_ => state.Score = 0)).Absolute(20, 8),
    ]))
    .Build();

await terminal.RunAsync();

class OverlayState
{
    public int Score { get; set; }
}
