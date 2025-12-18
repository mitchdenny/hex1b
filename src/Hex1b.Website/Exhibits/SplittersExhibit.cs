using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// An exhibit demonstrating horizontal and vertical splitters.
/// </summary>
public class SplittersExhibit(ILogger<SplittersExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<SplittersExhibit> _logger = logger;

    public override string Id => "splitters";
    public override string Title => "Splitters";
    public override string Description => "Horizontal and vertical splitters with resizable panes.";

    /// <summary>
    /// State for the splitters exhibit.
    /// </summary>
    private class SplittersState
    {
        private static readonly string[] ExampleIds = ["horizontal", "vertical", "nested-hv", "nested-vh", "quad"];
        
        public int SelectedExampleIndex { get; set; } = 0;
        public string SelectedExampleId => ExampleIds[SelectedExampleIndex];
        
        public IReadOnlyList<string> ExampleItems { get; } =
        [
            "Horizontal Split",
            "Vertical Split",
            "Nested (H inside V)",
            "Nested (V inside H)",
            "Quad Split",
        ];
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating splitters exhibit widget builder");

        var state = new SplittersState();

        return () =>
        {
            var ctx = new RootContext();

            var widget = ctx.Splitter(
                ctx.Panel(leftPanel => [
                    leftPanel.VStack(left => [
                        left.Text("Splitter Examples"),
                        left.Text("─────────────────"),
                        left.List(state.ExampleItems, e => state.SelectedExampleIndex = e.SelectedIndex, null),
                        left.Text(""),
                        left.Text("Use ↑↓ to select"),
                        left.Text("Tab to focus splitter"),
                        left.Text("←→ or ↑↓ to resize"),
                    ])
                ]),
                BuildExampleContent(ctx, state.SelectedExampleId),
                leftWidth: 22
            );

            return widget;
        };
    }

    private static Hex1bWidget BuildExampleContent(RootContext ctx, string exampleId)
    {
        return exampleId switch
        {
            "horizontal" => BuildHorizontalExample(ctx),
            "vertical" => BuildVerticalExample(ctx),
            "nested-hv" => BuildNestedHVExample(ctx),
            "nested-vh" => BuildNestedVHExample(ctx),
            "quad" => BuildQuadExample(ctx),
            _ => BuildHorizontalExample(ctx)
        };
    }

    private static Hex1bWidget BuildHorizontalExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.Splitter(
                ctx.Panel(left => [
                    left.VStack(v => [
                        v.Text("═══ Left Pane ═══"),
                        v.Text(""),
                        v.Text("This is the left side", TextOverflow.Wrap),
                        v.Text("of a horizontal split.", TextOverflow.Wrap),
                        v.Text(""),
                        v.Text("The splitter uses a", TextOverflow.Wrap),
                        v.Text("vertical divider (│)", TextOverflow.Wrap),
                        v.Text("between panes.", TextOverflow.Wrap),
                        v.Text(""),
                        v.Text("Tab to the splitter,", TextOverflow.Wrap),
                        v.Text("then use ← → arrows", TextOverflow.Wrap),
                        v.Text("to resize.", TextOverflow.Wrap),
                    ])
                ]),
                ctx.Panel(right => [
                    right.VStack(v => [
                        v.Text("═══ Right Pane ═══"),
                        v.Text(""),
                        v.Text("This is the right side", TextOverflow.Wrap),
                        v.Text("of the horizontal split.", TextOverflow.Wrap),
                        v.Text(""),
                        v.Text("Both panes share the", TextOverflow.Wrap),
                        v.Text("full height of the", TextOverflow.Wrap),
                        v.Text("container.", TextOverflow.Wrap),
                    ])
                ]),
                leftWidth: 28
            ),
            title: "Horizontal Splitter (Left │ Right)"
        );
    }

    private static Hex1bWidget BuildVerticalExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VSplitter(
                ctx.Panel(top => [
                    top.VStack(v => [
                        v.Text("═══ Top Pane ═══"),
                        v.Text(""),
                        v.Text("This is the top section of a vertical split.", TextOverflow.Wrap),
                        v.Text("The splitter uses a horizontal divider (───) between panes.", TextOverflow.Wrap),
                    ])
                ]),
                ctx.Panel(bottom => [
                    bottom.VStack(v => [
                        v.Text("═══ Bottom Pane ═══"),
                        v.Text(""),
                        v.Text("This is the bottom section. Tab to the splitter, then", TextOverflow.Wrap),
                        v.Text("use ↑ ↓ arrows to resize the top/bottom panes.", TextOverflow.Wrap),
                        v.Text(""),
                        v.Text("Vertical splits are great for:", TextOverflow.Wrap),
                        v.Text("• Code editor + terminal", TextOverflow.Wrap),
                        v.Text("• Main content + status bar", TextOverflow.Wrap),
                        v.Text("• Preview + properties", TextOverflow.Wrap),
                    ])
                ]),
                topHeight: 6
            ),
            title: "Vertical Splitter (Top ─ Bottom)"
        );
    }

    private static Hex1bWidget BuildNestedHVExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VSplitter(
                // Top: horizontal splitter
                ctx.Splitter(
                    ctx.Panel(tl => [
                        tl.VStack(v => [
                            v.Text("Top-Left"),
                            v.Text(""),
                            v.Text("Horizontal", TextOverflow.Wrap),
                            v.Text("inside top", TextOverflow.Wrap),
                        ])
                    ]),
                    ctx.Panel(tr => [
                        tr.VStack(v => [
                            v.Text("Top-Right"),
                            v.Text(""),
                            v.Text("Both panes", TextOverflow.Wrap),
                            v.Text("share height", TextOverflow.Wrap),
                        ])
                    ]),
                    leftWidth: 20
                ),
                // Bottom: single panel
                ctx.Panel(bottom => [
                    bottom.VStack(v => [
                        v.Text("═══ Bottom Pane ═══"),
                        v.Text(""),
                        v.Text("This demonstrates nesting a horizontal splitter inside the", TextOverflow.Wrap),
                        v.Text("top pane of a vertical splitter. Great for editor layouts!", TextOverflow.Wrap),
                    ])
                ]),
                topHeight: 8
            ),
            title: "Nested: Horizontal inside Vertical"
        );
    }

    private static Hex1bWidget BuildNestedVHExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.Splitter(
                // Left: single panel
                ctx.Panel(left => [
                    left.VStack(v => [
                        v.Text("═══ Left ═══"),
                        v.Text(""),
                        v.Text("Sidebar", TextOverflow.Wrap),
                        v.Text("content", TextOverflow.Wrap),
                        v.Text("goes here", TextOverflow.Wrap),
                        v.Text(""),
                        v.Text("This is the", TextOverflow.Wrap),
                        v.Text("outer left", TextOverflow.Wrap),
                        v.Text("pane of a", TextOverflow.Wrap),
                        v.Text("horizontal", TextOverflow.Wrap),
                        v.Text("splitter.", TextOverflow.Wrap),
                    ])
                ]),
                // Right: vertical splitter
                ctx.VSplitter(
                    ctx.Panel(rt => [
                        rt.VStack(v => [
                            v.Text("Right-Top"),
                            v.Text(""),
                            v.Text("Vertical splitter", TextOverflow.Wrap),
                            v.Text("inside right pane", TextOverflow.Wrap),
                        ])
                    ]),
                    ctx.Panel(rb => [
                        rb.VStack(v => [
                            v.Text("Right-Bottom"),
                            v.Text(""),
                            v.Text("Great for file tree + editor + terminal layouts!", TextOverflow.Wrap),
                        ])
                    ]),
                    topHeight: 6
                ),
                leftWidth: 18
            ),
            title: "Nested: Vertical inside Horizontal"
        );
    }

    private static Hex1bWidget BuildQuadExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VSplitter(
                // Top row: horizontal splitter
                ctx.Splitter(
                    ctx.Panel(tl => [
                        tl.VStack(v => [
                            v.Text("┌─ Quad 1 ─┐"),
                            v.Text("Top-Left"),
                            v.Text(""),
                            v.Text("Navigator", TextOverflow.Wrap),
                        ])
                    ]),
                    ctx.Panel(tr => [
                        tr.VStack(v => [
                            v.Text("┌─ Quad 2 ─┐"),
                            v.Text("Top-Right"),
                            v.Text(""),
                            v.Text("Editor", TextOverflow.Wrap),
                        ])
                    ]),
                    leftWidth: 20
                ),
                // Bottom row: horizontal splitter
                ctx.Splitter(
                    ctx.Panel(bl => [
                        bl.VStack(v => [
                            v.Text("┌─ Quad 3 ─┐"),
                            v.Text("Bottom-Left"),
                            v.Text(""),
                            v.Text("Terminal", TextOverflow.Wrap),
                        ])
                    ]),
                    ctx.Panel(br => [
                        br.VStack(v => [
                            v.Text("┌─ Quad 4 ─┐"),
                            v.Text("Bottom-Right"),
                            v.Text(""),
                            v.Text("Output", TextOverflow.Wrap),
                        ])
                    ]),
                    leftWidth: 20
                ),
                topHeight: 8
            ),
            title: "Quad Split (4 panes)"
        );
    }
}
