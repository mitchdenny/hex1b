using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// A basic example demonstrating vertical scrolling.
/// </summary>
public class ScrollBasicExample(ILogger<ScrollBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ScrollBasicExample> _logger = logger;

    public override string Id => "scroll-basic";
    public override string Title => "Scroll - Basic";
    public override string Description => "Basic vertical scrolling with scrollbar indicator.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scroll basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();

            return ctx.Border(
                ctx.VScrollPanel(
                    v => [
                        v.Text("═══ Scrollable Content ═══"),
                        v.Text(""),
                        v.Text("This content scrolls vertically."),
                        v.Text("Use arrow keys ↑↓ to scroll."),
                        v.Text(""),
                        v.Text("Line 6"),
                        v.Text("Line 7"),
                        v.Text("Line 8"),
                        v.Text("Line 9"),
                        v.Text("Line 10"),
                        v.Text("Line 11"),
                        v.Text("Line 12"),
                        v.Text(""),
                        v.Text("── End of Content ──")
                    ]
                )
            ).Title("Scroll Demo");
        };
    }
}
