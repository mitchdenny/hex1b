using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Welcome to Hex1b"),
        v.Text("Build beautiful terminal UIs")
    ]))
    .Build();

await terminal.RunAsync();
