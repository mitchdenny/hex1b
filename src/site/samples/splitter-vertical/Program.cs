using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VSplitter(
        ctx.VStack(top => [
            top.VStack(v => [
                v.Text("Top Pane"),
                v.Text(""),
                v.Text("This is the top section of a vertical split.").Wrap()
            ])
        ]),
        ctx.VStack(bottom => [
            bottom.VStack(v => [
                v.Text("Bottom Pane"),
                v.Text(""),
                v.Text("This is the bottom section. Tab to the splitter,").Wrap(),
                v.Text("then use ↑ ↓ to resize the top/bottom panes.").Wrap(),
                v.Text(""),
                v.Text("Great for editor + terminal layouts.").Wrap()
            ])
        ]),
        topHeight: 5
    ))
    .Build();

await terminal.RunAsync();
