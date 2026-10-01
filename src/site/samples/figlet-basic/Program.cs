using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.FigletText("Hello").Font(FigletFonts.Standard),
        v.Text(""),
        v.Text("Press Ctrl+C to exit.")
    ]))
    .Build();

await terminal.RunAsync();
