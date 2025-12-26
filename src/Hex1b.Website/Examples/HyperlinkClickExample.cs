using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class HyperlinkClickExample(ILogger<HyperlinkClickExample> logger) : Hex1bExample
{
    private readonly ILogger<HyperlinkClickExample> _logger = logger;
    private int _clickCount;

    public override string Id => "hyperlink-click";
    public override string Title => "Hyperlink Click Handler";
    public override string Description => "Demonstrates handling hyperlink click events";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hyperlink click example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text($"Link clicked {_clickCount} times"),
                v.Text(""),
                v.Hyperlink("Click me!", "https://example.com")
                    .OnClick(e => _clickCount++),
                v.Text(""),
                v.Text("Press Enter on the link or click it")
            ]);
        };
    }
}
