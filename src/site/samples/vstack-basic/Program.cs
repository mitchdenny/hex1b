using Hex1b;

var state = new AppState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Welcome to My App"),
        v.Text(""),
        v.Button("Start").OnClick(_ => state.Start()),
        v.Button("Settings").OnClick(_ => state.ShowSettings()),
        v.Button("Quit").OnClick(args => args.Context.RequestStop())
    ]))
    .Build();

await terminal.RunAsync();

class AppState
{
    public void Start() { /* ... */ }
    public void ShowSettings() { /* ... */ }
}
