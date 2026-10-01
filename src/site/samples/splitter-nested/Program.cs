using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VSplitter(
        // Top: horizontal splitter
        ctx.HSplitter(
            ctx.VStack(tl => [
                tl.VStack(v => [
                    v.Text("Top-Left"),
                    v.Text(""),
                    v.Text("Horizontal split").Wrap(),
                    v.Text("in top pane").Wrap()
                ])
            ]),
            ctx.VStack(tr => [
                tr.VStack(v => [
                    v.Text("Top-Right"),
                    v.Text(""),
                    v.Text("Both panes share").Wrap(),
                    v.Text("the same height").Wrap()
                ])
            ]),
            leftWidth: 20
        ),
        // Bottom: single panel
        ctx.VStack(bottom => [
            bottom.VStack(v => [
                v.Text("Bottom Pane"),
                v.Text(""),
                v.Text("This demonstrates nesting a horizontal splitter").Wrap(),
                v.Text("inside the top pane of a vertical splitter.").Wrap(),
                v.Text("Great for IDE-style layouts!").Wrap()
            ])
        ]),
        topHeight: 6
    ))
    .Build();

await terminal.RunAsync();
