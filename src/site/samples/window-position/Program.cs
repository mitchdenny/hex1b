using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Text("  Click buttons to open windows at different positions:"),
                v.Text(""),
                v.HStack(h => [
                    h.Text("  "),
                    h.Button("Top-Left").OnClick(e => {
                        e.Windows.Window(w => w.Text("  Top-Left  "))
                            .Title("TL")
                            .Size(15, 5)
                            .Position(WindowPositionSpec.TopLeft)
                            .Open(e.Windows);
                    }),
                    h.Text(" "),
                    h.Button("Center").OnClick(e => {
                        e.Windows.Window(w => w.Text("  Center  "))
                            .Title("C")
                            .Size(15, 5)
                            .Position(WindowPositionSpec.Center)
                            .Open(e.Windows);
                    }),
                    h.Text(" "),
                    h.Button("Bottom-Right").OnClick(e => {
                        e.Windows.Window(w => w.Text("  Bottom-Right  "))
                            .Title("BR")
                            .Size(18, 5)
                            .Position(WindowPositionSpec.BottomRight)
                            .Open(e.Windows);
                    })
                ]),
                v.Text(""),
                v.Button("Close All").OnClick(e => e.Windows.CloseAll())
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();
