using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Example demonstrating a basic horizontal splitter.
/// </summary>
public class SplitterBasicExample(ILogger<SplitterBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<SplitterBasicExample> _logger = logger;

    public override string Id => "splitter-basic";
    public override string Title => "Splitter Widget - Horizontal Split";
    public override string Description => "A basic horizontal splitter dividing content into left and right panes.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating basic splitter example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HSplitter(
                ctx.ThemingPanel(theme => theme, left => [
                    left.VStack(v => [
                        v.Text("Left Pane"),
                        v.Text(""),
                        v.Text("This is the left side").Wrap(),
                        v.Text("of a horizontal split.").Wrap(),
                        v.Text(""),
                        v.Text("Tab to focus the splitter,").Wrap(),
                        v.Text("then use ← → to resize.").Wrap()
                    ])
                ]),
                ctx.ThemingPanel(theme => theme, right => [
                    right.VStack(v => [
                        v.Text("Right Pane"),
                        v.Text(""),
                        v.Text("This is the right side").Wrap(),
                        v.Text("of the horizontal split.").Wrap(),
                        v.Text(""),
                        v.Text("Both panes share the").Wrap(),
                        v.Text("full height.").Wrap()
                    ])
                ]),
                leftWidth: 25
            );
        };
    }
}
