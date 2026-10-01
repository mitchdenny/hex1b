using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Button("Open Window").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text("  Hello from a floating window!"),
                        v.Text(""),
                        v.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                    ]))
                    .Title("My Window")
                    .Size(40, 8)
                    .Open(e.Windows);
                }),
                v.Text(""),
                v.Text("  Press the button to open a window...")
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();
