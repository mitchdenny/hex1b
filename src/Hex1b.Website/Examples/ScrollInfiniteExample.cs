using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example demonstrating infinite scroll pattern - loading more items when near the end.
/// </summary>
public class ScrollInfiniteExample(ILogger<ScrollInfiniteExample> logger) : Hex1bExample
{
    private readonly ILogger<ScrollInfiniteExample> _logger = logger;

    public override string Id => "scroll-infinite";
    public override string Title => "Scroll - Infinite Scroll";
    public override string Description => "Load more items when scrolling near the end.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scroll infinite example widget builder");

        var loadedItems = Enumerable.Range(1, 20).Select(i => $"Item {i}").ToList();
        int loadCount = 1;
        string status = "Scroll down to load more...";

        return () =>
        {
            var ctx = new RootContext();

            return ctx.VStack(v => [
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
                    })
                ).Title("Infinite Scroll")
            ]);
        };
    }
}
