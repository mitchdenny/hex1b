using Hex1b;

var state = new ButtonState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Button Examples"),
        v.Text(""),
        v.Text($"Button clicked {state.ClickCount} times"),
        v.Text(""),
        v.Button("Click me!").OnClick(_ => state.ClickCount++),
        v.Text(""),
        v.Text("Press Tab to focus, Enter or Space to activate")
    ]))
    .Build();

await terminal.RunAsync();

class ButtonState
{
    public int ClickCount { get; set; }
}
