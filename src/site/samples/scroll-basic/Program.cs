using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(
        ctx.VScrollPanel(
            v => [
                v.Text("═══ Scrollable Content ═══"),
                v.Text(""),
                v.Text("This content scrolls vertically."),
                v.Text("Use arrow keys ↑↓ to scroll."),
                v.Text(""),
                v.Text("Line 6"),
                v.Text("Line 7"),
                v.Text("Line 8"),
                v.Text("Line 9"),
                v.Text("Line 10"),
                v.Text("Line 11"),
                v.Text("Line 12"),
                v.Text(""),
                v.Text("── End of Content ──")
            ]
        )).Title("Scroll Demo"))
    .Build();

await terminal.RunAsync();
