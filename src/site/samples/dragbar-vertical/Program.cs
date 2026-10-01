using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Border(
            v.VStack(main => [
                main.Text(""),
                main.Text("  Main Content Area"),
                main.Text(""),
                main.Text("  The panel below can be resized").Wrap(),
                main.Text("  by dragging its top handle.").Wrap()
            ])).Title("Editor").Fill(),

        v.DragBarPanel(
            v.VStack(panel => [
                panel.Text(" Output Panel"),
                panel.Text(" [INFO] Build started..."),
                panel.Text(" [INFO] Compilation successful"),
                panel.Text(" [INFO] 0 warnings, 0 errors")
            ])
        )
        .InitialSize(8)
        .MinSize(4)
        .MaxSize(20)
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
