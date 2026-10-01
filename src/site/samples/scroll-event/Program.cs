using Hex1b;

int currentOffset = 0;
int maxOffset = 0;
int contentSize = 0;
int viewportSize = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text($"Position: {currentOffset}/{maxOffset}"),
        v.Text($"Content: {contentSize} lines, Viewport: {viewportSize} lines"),
        v.Text(""),
        v.Border(
            v.VScrollPanel(
                inner => [
                    inner.Text("Line 1 - Scroll to see position update"),
                    inner.Text("Line 2"),
                    inner.Text("Line 3"),
                    inner.Text("Line 4"),
                    inner.Text("Line 5"),
                    inner.Text("Line 6"),
                    inner.Text("Line 7"),
                    inner.Text("Line 8"),
                    inner.Text("Line 9"),
                    inner.Text("Line 10"),
                    inner.Text("Line 11"),
                    inner.Text("Line 12 - End"),
                ]
            ).OnScroll(args => {
                currentOffset = args.Offset;
                maxOffset = args.MaxOffset;
                contentSize = args.ContentSize;
                viewportSize = args.ViewportSize;
            })).Title("Scrollable Area")
    ]))
    .Build();

await terminal.RunAsync();
