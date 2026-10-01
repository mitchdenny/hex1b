using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.HStack(h => [
        h.DragBarPanel(
            h.VStack(panel => [
                panel.Text(" Sidebar"),
                panel.Text(" ───────"),
                panel.Text(" Drag the handle →"),
                panel.Text(" or Tab to it and"),
                panel.Text(" use ← → arrow keys")
            ])
        )
        .InitialSize(30)
        .MinSize(15)
        .MaxSize(50),

        h.Border(
            h.VStack(main => [
                main.Text(""),
                main.Text("  Main Content"),
                main.Text(""),
                main.Text("  This area fills the remaining space.").Wrap(),
                main.Text("  Resize the sidebar by dragging the").Wrap(),
                main.Text("  handle or using arrow keys.").Wrap()
            ])).Title("Content").Fill()
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
