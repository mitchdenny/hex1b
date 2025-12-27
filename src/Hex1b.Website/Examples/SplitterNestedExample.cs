using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Example demonstrating nested splitters.
/// </summary>
public class SplitterNestedExample(ILogger<SplitterNestedExample> logger) : Hex1bExample
{
    private readonly ILogger<SplitterNestedExample> _logger = logger;

    public override string Id => "splitter-nested";
    public override string Title => "Splitter Widget - Nested Splitters";
    public override string Description => "Demonstrates nesting splitters to create complex multi-pane layouts.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating nested splitter example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VSplitter(
                // Top: horizontal splitter
                ctx.HSplitter(
                    ctx.ThemingPanel(theme => theme, tl => [
                        tl.VStack(v => [
                            v.Text("Top-Left"),
                            v.Text(""),
                            v.Text("Horizontal split").Wrap(),
                            v.Text("in top pane").Wrap()
                        ])
                    ]),
                    ctx.ThemingPanel(theme => theme, tr => [
                        tr.VStack(v => [
                            v.Text("Top-Right"),
                            v.Text(""),
                            v.Text("Both panes share").Wrap(),
                            v.Text("the same height").Wrap()
                        ])
                    ]),
                    leftWidth: 20
                ),
                // Bottom: single panel
                ctx.ThemingPanel(theme => theme, bottom => [
                    bottom.VStack(v => [
                        v.Text("Bottom Pane"),
                        v.Text(""),
                        v.Text("This demonstrates nesting a horizontal splitter").Wrap(),
                        v.Text("inside the top pane of a vertical splitter.").Wrap(),
                        v.Text("Great for IDE-style layouts!").Wrap()
                    ])
                ]),
                topHeight: 6
            );
        };
    }
}
