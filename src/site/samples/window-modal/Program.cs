using Hex1b;

var lastResult = "No dialog shown yet";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text($"Last Result: {lastResult}"),
                v.Text(""),
                v.Button("Confirm Delete").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text("  🗑️  Delete this item?"),
                        v.Text("  This action cannot be undone."),
                        v.Text(""),
                        v.HStack(h => [
                            h.Text("  "),
                            h.Button("Delete").OnClick(_ => w.Window.CloseWithResult(true)),
                            h.Text(" "),
                            h.Button("Cancel").OnClick(_ => w.Window.CloseWithResult(false))
                        ])
                    ]))
                    .Title("Confirm Delete")
                    .Size(40, 9)
                    .Modal()
                    .OnResult<bool>(result => {
                        if (result.IsCancelled || !result.Value)
                            lastResult = "Cancelled";
                        else
                            lastResult = "Deleted!";
                    })
                    .Open(e.Windows);
                })
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();
