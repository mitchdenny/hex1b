using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.Align(Alignment.Center,
            b.Text("Hello, World!")
        )
    ]).Title("Centered Content"))
    .Build();

await terminal.RunAsync();
