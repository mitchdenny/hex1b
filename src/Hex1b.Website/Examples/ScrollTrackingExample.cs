using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example demonstrating scroll position tracking for UI display.
/// </summary>
public class ScrollTrackingExample(ILogger<ScrollTrackingExample> logger) : Hex1bExample
{
    private readonly ILogger<ScrollTrackingExample> _logger = logger;

    public override string Id => "scroll-tracking";
    public override string Title => "Scroll - Position Tracking";
    public override string Description => "Track scroll position to display in the UI.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scroll tracking example widget builder");

        var items = Enumerable.Range(1, 50).Select(i => $"Item {i}").ToList();
        int scrollPosition = 0;
        int viewportSize = 0;

        return () =>
        {
            var ctx = new RootContext();
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
                    }),
                    title: "Scrollable List"
                )
            ]);
        };
    }
}
