using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Text("Main content area"),
            b.Text(""),
            b.Text("The status bar sits at the bottom of the window")
        ]).Title("Application").FillHeight(),
        v.InfoBar(s => [
            s.Section("NORMAL"),
            s.Section("main.cs"),
            s.Section("Ln 42, Col 8")
        ]).Divider(" │ ")
    ]))
    .Build();

await terminal.RunAsync();
