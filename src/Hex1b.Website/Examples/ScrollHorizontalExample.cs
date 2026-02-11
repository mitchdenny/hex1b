using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example demonstrating horizontal scrolling.
/// </summary>
public class ScrollHorizontalExample(ILogger<ScrollHorizontalExample> logger) : Hex1bExample
{
    private readonly ILogger<ScrollHorizontalExample> _logger = logger;

    public override string Id => "scroll-horizontal";
    public override string Title => "Scroll - Horizontal";
    public override string Description => "Horizontal scrolling for wide content.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scroll horizontal example widget builder");

        return () =>
        {
            var ctx = new RootContext();

            return ctx.Border(
                ctx.VStack(v => [
                    v.Text("Wide content below - use ← → to scroll:"),
                    v.Text(""),
                    v.HScrollPanel(
                        h => [
                            h.Text("START | Column 1 | Column 2 | Column 3 | Column 4 | Column 5 | Column 6 | Column 7 | Column 8 | END"),
                        ]
                    ),
                ])
            ).Title("Horizontal Scroll");
        };
    }
}
