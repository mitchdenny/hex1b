using Hex1b;

var pinned = false;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Text("  Click the button below to open a window"),
                v.Text(""),
                v.Button("Open Window with Actions").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text(pinned ? "  📌 This window is pinned!" : "  Click the pin icon above"),
                        v.Text("")
                    ]))
                    .Title("Custom Actions")
                    .Size(40, 7)
                    .LeftTitleActions(t => [
                        t.Action("📌", _ => pinned = !pinned),
                        t.Action("📋", _ => { /* copy logic */ })
                    ])
                    .RightTitleActions(t => [
                        t.Action("?", _ => { /* help logic */ }),
                        t.Close()
                    ])
                    .Open(e.Windows);
                })
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();
