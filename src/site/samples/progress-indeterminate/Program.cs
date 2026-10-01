using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Loading..."),
        v.ProgressIndeterminate()  // Self-animating!
    ]))
    .Build();

await terminal.RunAsync();
