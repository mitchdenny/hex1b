using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.HSplitter(
        ctx.VStack(left => [
            left.VStack(v => [
                v.Text("Left Pane"),
                v.Text(""),
                v.Text("This is the left side").Wrap(),
                v.Text("of a horizontal split.").Wrap(),
                v.Text(""),
                v.Text("Tab to focus the splitter,").Wrap(),
                v.Text("then use ← → to resize.").Wrap()
            ])
        ]),
        ctx.VStack(right => [
            right.VStack(v => [
                v.Text("Right Pane"),
                v.Text(""),
                v.Text("This is the right side").Wrap(),
                v.Text("of the horizontal split.").Wrap(),
                v.Text(""),
                v.Text("Both panes share the").Wrap(),
                v.Text("full height.").Wrap()
            ])
        ]),
        leftWidth: 25
    ))
    .Build();

await terminal.RunAsync();
