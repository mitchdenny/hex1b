using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example demonstrating scroll event handling.
/// </summary>
public class ScrollEventExample(ILogger<ScrollEventExample> logger) : Hex1bExample
{
    private readonly ILogger<ScrollEventExample> _logger = logger;

    public override string Id => "scroll-event";
    public override string Title => "Scroll - Events";
    public override string Description => "Scroll widget with event handling to track position.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scroll event example widget builder");

        int currentOffset = 0;
        int maxOffset = 0;
        int contentSize = 0;
        int viewportSize = 0;

        return () =>
        {
            var ctx = new RootContext();

            return ctx.VStack(v => [
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
                    })
                ).Title("Scrollable Area")
            ]);
        };
    }
}
