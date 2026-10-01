using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(
        ctx.VStack(v => [
            v.Text("Wide content below - use ← → to scroll:"),
            v.Text(""),
            v.HScrollPanel(
                h => [
                    h.Text("START | Column 1 | Column 2 | Column 3 | Column 4 | Column 5 | Column 6 | Column 7 | Column 8 | END"),
                ]
            ),
        ])).Title("Horizontal Scroll"))
    .Build();

await terminal.RunAsync();
