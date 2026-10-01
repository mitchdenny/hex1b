using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.HStack(h => [
        h.Icon("🏠"),
        h.Text(" Home"),
        h.Text("  |  "),
        h.Icon("⚙️"),
        h.Text(" Settings"),
        h.Text("  |  "),
        h.Icon("❓"),
        h.Text(" Help")
    ]))
    .Build();

await terminal.RunAsync();
