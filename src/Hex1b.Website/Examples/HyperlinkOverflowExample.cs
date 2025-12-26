using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class HyperlinkOverflowExample(ILogger<HyperlinkOverflowExample> logger) : Hex1bExample
{
    private readonly ILogger<HyperlinkOverflowExample> _logger = logger;

    public override string Id => "hyperlink-overflow";
    public override string Title => "Hyperlink Widget - Overflow Modes";
    public override string Description => "Demonstrates hyperlink text overflow handling";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hyperlink overflow example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("═══ Hyperlink Overflow Modes ═══"),
                v.Text(""),
                v.Text("Default (Truncate):"),
                v.Hyperlink(
                    "This is a very long hyperlink text that will be truncated when it exceeds the width",
                    "https://example.com"
                ),
                v.Text(""),
                v.Text("Wrap Mode:"),
                v.Hyperlink(
                    "This hyperlink has wrapping enabled so the text will break across multiple lines at word boundaries when needed",
                    "https://example.com"
                ).Wrap(),
                v.Text(""),
                v.Text("Ellipsis Mode:"),
                v.Hyperlink(
                    "This hyperlink shows ellipsis when text is too long to fit in the available space",
                    "https://example.com"
                ).Ellipsis().FixedWidth(50)
            ]);
        };
    }
}
