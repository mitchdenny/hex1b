using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.Text("Welcome to Hex1b!"),
        b.Text(""),
        b.Text("This content is wrapped"),
        b.Text("in a border widget.")
    ]))
    .Build();

await terminal.RunAsync();
