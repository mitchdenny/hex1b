using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx =>
        ctx.Border(b => [
            b.Text("Hello from Hex1b!"),
            b.Button("Click me").OnClick(_ => Console.Beep())
        ]).Title("My App"))
    .Build();

await terminal.RunAsync();
