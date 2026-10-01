using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("═══ Text Overflow Modes ═══"),
        v.Text(""),
        v.Text("Wrap Mode:"),
        v.Text(
            "This is a long description that demonstrates text wrapping behavior in Hex1b. " +
            "When the text content exceeds the available width of the container, it automatically " +
            "breaks at word boundaries to fit within the allocated space. This ensures that all " +
            "content remains visible to the user without requiring horizontal scrolling. The widget's " +
            "measured height increases dynamically based on the number of wrapped lines."
        ).Wrap(),
        v.Text(""),
        v.Text("Ellipsis Mode:"),
        v.Text(
            "This is a much longer piece of text that will definitely " +
            "be truncated with an ellipsis character sequence when it " +
            "exceeds the available fixed width of forty columns"
        ).Ellipsis().FixedWidth(40),
        v.Text(""),
        v.Text("Default (Truncate) Mode:"),
        v.Text(
            "This text extends beyond its allocated bounds and " +
            "will be clipped by the parent container if clipping is enabled"
        )
    ]))
    .Build();

await terminal.RunAsync();
