using Hex1b;

var items = Enumerable.Range(1, 50).Select(i => $"Item {i}").ToList();
int scrollPosition = 0;
int viewportSize = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx =>
    {
        var totalContent = items.Count;
        var endVisible = Math.Min(scrollPosition + viewportSize, totalContent);
        
        return ctx.VStack(v => [
            v.Text($"Viewing: {scrollPosition + 1} - {endVisible} of {totalContent}"),
            v.Text(""),
            v.Border(
                v.VScrollPanel(
                    inner => items.Select(item => inner.Text(item)).ToArray()
                ).OnScroll(args => {
                    scrollPosition = args.Offset;
                    viewportSize = args.ViewportSize;
                })).Title("Scrollable List")
        ]);
    })
    .Build();

await terminal.RunAsync();
