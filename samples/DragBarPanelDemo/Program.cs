using Hex1b;
using Hex1b.Widgets;

var currentSize = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.VStack(outer => [
            outer.Text(" DragBarPanel Demo").FixedHeight(1),
            outer.Text("─────────────────────────────────────────────────").FixedHeight(1),

            outer.HStack(main => [
                // Left panel — resizable via drag bar on its right edge
                main.DragBarPanel(
                    main.VStack(panel => [
                        panel.Text(" Left Panel"),
                        panel.Text(" ───────────"),
                        panel.Text(" Drag the handle →"),
                        panel.Text(" or focus it and"),
                        panel.Text(" use ← → arrow keys"),
                        panel.Text(""),
                        panel.Text($" Width: {currentSize}")
                    ])
                )
                .InitialSize(30)
                .MinSize(15)
                .MaxSize(60)
                .OnSizeChanged(size => currentSize = size),

                // Center content — fills remaining space
                main.Border(
                    main.VStack(center => [
                        center.Text(""),
                        center.Text("  Main Content Area"),
                        center.Text(""),
                        center.Text("  This area fills the remaining space."),
                        center.Text("  The panels on either side can be resized"),
                        center.Text("  by dragging their handles or using arrow"),
                        center.Text("  keys when the handle is focused."),
                        center.Text(""),
                        center.Text("  Tab to navigate between focusable elements."),
                        center.Text(""),
                        center.Button("Exit").OnClick(e => e.Context.RequestStop())
                    ]),
                    title: "Content"
                ).Fill(),

                // Right panel — resizable via drag bar on its left edge (auto-detected)
                main.DragBarPanel(
                    main.VStack(panel => [
                        panel.Text(" Right Panel"),
                        panel.Text(" ───────────"),
                        panel.Text(" ← Drag handle"),
                        panel.Text(" Auto-detected"),
                        panel.Text(" left edge")
                    ])
                )
                .InitialSize(25)
                .MinSize(12)
                .MaxSize(50)
            ]).Fill(),

            outer.HStack(bottom => [
                // Bottom panel — resizable via drag bar on its top edge (auto-detected)
                bottom.DragBarPanel(
                    bottom.VStack(panel => [
                        panel.Text(" Bottom Panel — drag the top handle ↑ or use ↑↓ arrow keys"),
                        panel.Text(" [INFO] Build output goes here..."),
                        panel.Text(" [INFO] Compilation successful"),
                        panel.Text(" [INFO] 0 warnings, 0 errors")
                    ])
                )
                .InitialSize(8)
                .MinSize(4)
                .MaxSize(20)
            ]),

            outer.InfoBar([
                "Tab", "Navigate",
                "←→↑↓", "Resize",
                "Drag", "Resize handle",
                "Ctrl+C", "Exit"
            ])
        ])
    .WithInputBindings(bindings => { }))
    .WithMouse()
    .Build();

await terminal.RunAsync();
