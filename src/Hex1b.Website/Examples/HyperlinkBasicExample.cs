using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class HyperlinkBasicExample(ILogger<HyperlinkBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<HyperlinkBasicExample> _logger = logger;

    public override string Id => "hyperlink-basic";
    public override string Title => "Hyperlink Widget - Basic Usage";
    public override string Description => "Demonstrates basic hyperlink widget usage with clickable links";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hyperlink basic example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Hyperlink Examples"),
                v.Text(""),
                v.Hyperlink("Visit Hex1b Docs", "https://hex1b.dev"),
                v.Hyperlink("GitHub Repository", "https://github.com/mitchdenny/hex1b"),
                v.Text(""),
                v.Text("Press Tab to navigate, Enter to activate")
            ]);
        };
    }
}
