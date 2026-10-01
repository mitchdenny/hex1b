using Hex1b;

var statusMessage = "Ready";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Accordion(a => [
            a.Section(s => [
                s.Text("  src/"),
                s.Text("    Program.cs"),
                s.Text("    Utils.cs"),
            ]).Title("EXPLORER")
            .RightActions(ra => [
                ra.Icon("+").OnClick(_ => statusMessage = "New file..."),
                ra.Icon("⟳").OnClick(_ => statusMessage = "Refreshed"),
            ]),

            a.Section(s => [
                s.Text("  ▸ Properties"),
                s.Text("  ▸ Methods"),
            ]).Title("OUTLINE")
            .RightActions(ra => [
                ra.Icon("⟳").OnClick(_ => statusMessage = "Outline refreshed"),
            ]),

            a.Section(s => [
                s.Text("  main"),
                s.Text("  develop"),
            ]).Title("SOURCE CONTROL")
            .LeftActions(la => [
                la.Toggle("▶", "▼"),
                la.Icon("✓").OnClick(_ => statusMessage = "Committed"),
            ])
            .RightActions(ra => [
                ra.Icon("⟳").OnClick(_ => statusMessage = "Pulling..."),
            ]),
        ]),
        v.Text(""),
        v.Text($" Status: {statusMessage}"),
    ]))
    .Build();

await terminal.RunAsync();
