using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Button("Open Resizable Window").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text("  Drag edges or corners to resize!"),
                        v.Text(""),
                        v.Text("  Constraints:"),
                        v.Text("  • Min: 30×8"),
                        v.Text("  • Max: 80×20"),
                        v.Text("")
                    ]).Fill())
                    .Title("Resizable Window")
                    .Size(50, 12)
                    .Resizable(minWidth: 30, minHeight: 8, maxWidth: 80, maxHeight: 20)
                    .Open(e.Windows);
                }),
                v.Text(""),
                v.Text("  Drag window edges to resize")
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();
