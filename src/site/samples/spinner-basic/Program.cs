using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.HStack(h => [
        h.Spinner(),
        h.Text(" Loading...")
    ]))
    .Build();

await terminal.RunAsync();
