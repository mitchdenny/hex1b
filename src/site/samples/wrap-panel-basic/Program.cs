using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.WrapPanel(w => [
        w.Border(b => [b.Text("Item 1")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 2")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 3")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 4")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 5")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 6")]).FixedWidth(15),
    ]))
    .Build();

await terminal.RunAsync();
