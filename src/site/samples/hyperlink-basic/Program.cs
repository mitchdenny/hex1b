using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("Hyperlink Examples"),
        v.Text(""),
        v.Hyperlink("Visit Hex1b Docs", "https://hex1b.dev"),
        v.Hyperlink("GitHub Repository", "https://github.com/mitchdenny/hex1b"),
        v.Text(""),
        v.Text("Press Tab to navigate, Enter to activate")
    ]))
    .Build();

await terminal.RunAsync();
