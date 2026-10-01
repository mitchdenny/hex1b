using Hex1b;

var loadedItems = Enumerable.Range(1, 20).Select(i => $"Item {i}").ToList();
int loadCount = 1;
string status = "Scroll down to load more...";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text($"Loaded: {loadedItems.Count} items (batch {loadCount})"),
        v.Text(status),
        v.Text(""),
        v.Border(
            v.VScrollPanel(
                inner => loadedItems.Select(item => inner.Text(item)).ToArray()
            ).OnScroll(args => {
                // Load more when scrolled past 80%
                if (args.Progress > 0.8 && args.IsScrollable)
                {
                    loadCount++;
                    var startIndex = loadedItems.Count + 1;
                    var newItems = Enumerable.Range(startIndex, 10)
                        .Select(i => $"Item {i} (batch {loadCount})")
                        .ToList();
                    loadedItems.AddRange(newItems);
                    status = $"Loaded batch {loadCount}!";
                }
            })).Title("Infinite Scroll")
    ]))
    .Build();

await terminal.RunAsync();
