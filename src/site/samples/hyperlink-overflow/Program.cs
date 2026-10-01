using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text("═══ Hyperlink Overflow Modes ═══"),
        v.Text(""),
        v.Text("Default (Truncate):"),
        v.Hyperlink(
            "This is a very long hyperlink text that will be truncated when it exceeds the width",
            "https://example.com"
        ),
        v.Text(""),
        v.Text("Wrap Mode:"),
        v.Hyperlink(
            "This hyperlink has wrapping enabled so the text will break across " +
            "multiple lines at word boundaries when needed",
            "https://example.com"
        ).Wrap(),
        v.Text(""),
        v.Text("Ellipsis Mode:"),
        v.Hyperlink(
            "This hyperlink shows ellipsis when text is too long to fit in the available space",
            "https://example.com"
        ).Ellipsis().FixedWidth(50)
    ]))
    .Build();

await terminal.RunAsync();
