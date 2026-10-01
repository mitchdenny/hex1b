using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("standard:"),
        v.FigletText("Hex1b").Font(FigletFonts.Standard),
        v.Text(""),
        v.Text("slant:"),
        v.FigletText("Hex1b").Font(FigletFonts.Slant),
        v.Text(""),
        v.Text("small:"),
        v.FigletText("Hex1b").Font(FigletFonts.Small),
    ]))
    .Build();

await terminal.RunAsync();
