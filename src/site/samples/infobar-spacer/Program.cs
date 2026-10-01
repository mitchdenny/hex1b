using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Text("Content with a flexible status bar")
        ]).Title("Spacer Demo").FillHeight(),
        v.InfoBar(s => [
            s.Section("Mode: INSERT"),
            s.Spacer(),
            s.Section("100%"),
            s.Divider(" │ "),
            s.Section("UTF-8")
        ])
    ]))
    .Build();

await terminal.RunAsync();
