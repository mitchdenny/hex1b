using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Example demonstrating a vertical splitter.
/// </summary>
public class SplitterVerticalExample(ILogger<SplitterVerticalExample> logger) : Hex1bExample
{
    private readonly ILogger<SplitterVerticalExample> _logger = logger;

    public override string Id => "splitter-vertical";
    public override string Title => "Splitter Widget - Vertical Split";
    public override string Description => "A vertical splitter dividing content into top and bottom panes.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating vertical splitter example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VSplitter(
                ctx.ThemingPanel(theme => theme, top => [
                    top.VStack(v => [
                        v.Text("Top Pane"),
                        v.Text(""),
                        v.Text("This is the top section of a vertical split.").Wrap()
                    ])
                ]),
                ctx.ThemingPanel(theme => theme, bottom => [
                    bottom.VStack(v => [
                        v.Text("Bottom Pane"),
                        v.Text(""),
                        v.Text("This is the bottom section. Tab to the splitter,").Wrap(),
                        v.Text("then use ↑ ↓ to resize the top/bottom panes.").Wrap(),
                        v.Text(""),
                        v.Text("Great for editor + terminal layouts.").Wrap()
                    ])
                ]),
                topHeight: 5
            );
        };
    }
}
