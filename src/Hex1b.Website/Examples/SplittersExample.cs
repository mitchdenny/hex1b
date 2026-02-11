using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An example demonstrating horizontal and vertical splitters.
/// </summary>
public class SplittersExample(ILogger<SplittersExample> logger) : Hex1bExample
{
    private readonly ILogger<SplittersExample> _logger = logger;

    public override string Id => "splitters";
    public override string Title => "Splitters";
    public override string Description => "Horizontal and vertical splitters with resizable panes.";

    /// <summary>
    /// State for the splitters example.
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
        _logger.LogInformation("Creating splitters example widget builder");

        var state = new SplittersState();

        return () =>
        {
            var ctx = new RootContext();

            var widget = ctx.HSplitter(
                ctx.VStack(left => [
                    left.Text("Splitter Examples"),
                    left.Text("─────────────────"),
                    left.List(state.ExampleItems).OnSelectionChanged(e => state.SelectedExampleIndex = e.SelectedIndex),
                    left.Text(""),
                    left.Text("Use ↑↓ to select"),
                    left.Text("Tab to focus splitter"),
                    left.Text("←→ or ↑↓ to resize"),
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
            ctx.HSplitter(
                ctx.VStack(v => [
                    v.Text("═══ Left Pane ═══"),
                    v.Text(""),
                    v.Text("This is the left side").Wrap(),
                    v.Text("of a horizontal split.").Wrap(),
                    v.Text(""),
                    v.Text("The splitter uses a").Wrap(),
                    v.Text("vertical divider (│)").Wrap(),
                    v.Text("between panes.").Wrap(),
                    v.Text(""),
                    v.Text("Tab to the splitter,").Wrap(),
                    v.Text("then use ← → arrows").Wrap(),
                    v.Text("to resize.").Wrap(),
                ]),
                ctx.VStack(v => [
                    v.Text("═══ Right Pane ═══"),
                    v.Text(""),
                    v.Text("This is the right side").Wrap(),
                    v.Text("of the horizontal split.").Wrap(),
                    v.Text(""),
                    v.Text("Both panes share the").Wrap(),
                    v.Text("full height of the").Wrap(),
                    v.Text("container.").Wrap(),
                ]),
                leftWidth: 28
            )
        ).Title("Horizontal Splitter (Left │ Right)");
    }

    private static Hex1bWidget BuildVerticalExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VSplitter(
                ctx.VStack(v => [
                    v.Text("═══ Top Pane ═══"),
                    v.Text(""),
                    v.Text("This is the top section of a vertical split.").Wrap(),
                    v.Text("The splitter uses a horizontal divider (───) between panes.").Wrap(),
                ]),
                ctx.VStack(v => [
                    v.Text("═══ Bottom Pane ═══"),
                    v.Text(""),
                    v.Text("This is the bottom section. Tab to the splitter, then").Wrap(),
                    v.Text("use ↑ ↓ arrows to resize the top/bottom panes.").Wrap(),
                    v.Text(""),
                    v.Text("Vertical splits are great for:").Wrap(),
                    v.Text("• Code editor + terminal").Wrap(),
                    v.Text("• Main content + status bar").Wrap(),
                    v.Text("• Preview + properties").Wrap(),
                ]),
                topHeight: 6
            )
        ).Title("Vertical Splitter (Top ─ Bottom)");
    }

    private static Hex1bWidget BuildNestedHVExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VSplitter(
                // Top: horizontal splitter
                ctx.HSplitter(
                    ctx.VStack(v => [
                        v.Text("Top-Left"),
                        v.Text(""),
                        v.Text("Horizontal").Wrap(),
                        v.Text("inside top").Wrap(),
                    ]),
                    ctx.VStack(v => [
                        v.Text("Top-Right"),
                        v.Text(""),
                        v.Text("Both panes").Wrap(),
                        v.Text("share height").Wrap(),
                    ]),
                    leftWidth: 20
                ),
                // Bottom: single panel
                ctx.VStack(v => [
                    v.Text("═══ Bottom Pane ═══"),
                    v.Text(""),
                    v.Text("This demonstrates nesting a horizontal splitter inside the").Wrap(),
                    v.Text("top pane of a vertical splitter. Great for editor layouts!").Wrap(),
                ]),
                topHeight: 8
            )
        ).Title("Nested: Horizontal inside Vertical");
    }

    private static Hex1bWidget BuildNestedVHExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.HSplitter(
                // Left: single panel
                ctx.VStack(v => [
                    v.Text("═══ Left ═══"),
                    v.Text(""),
                    v.Text("Sidebar").Wrap(),
                    v.Text("content").Wrap(),
                    v.Text("goes here").Wrap(),
                    v.Text(""),
                    v.Text("This is the").Wrap(),
                    v.Text("outer left").Wrap(),
                    v.Text("pane of a").Wrap(),
                    v.Text("horizontal").Wrap(),
                    v.Text("splitter.").Wrap(),
                ]),
                // Right: vertical splitter
                ctx.VSplitter(
                    ctx.VStack(v => [
                        v.Text("Right-Top"),
                        v.Text(""),
                        v.Text("Vertical splitter").Wrap(),
                        v.Text("inside right pane").Wrap(),
                    ]),
                    ctx.VStack(v => [
                        v.Text("Right-Bottom"),
                        v.Text(""),
                        v.Text("Great for file tree + editor + terminal layouts!").Wrap(),
                    ]),
                    topHeight: 6
                ),
                leftWidth: 18
            )
        ).Title("Nested: Vertical inside Horizontal");
    }

    private static Hex1bWidget BuildQuadExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VSplitter(
                // Top row: horizontal splitter
                ctx.HSplitter(
                    ctx.VStack(v => [
                        v.Text("┌─ Quad 1 ─┐"),
                        v.Text("Top-Left"),
                        v.Text(""),
                        v.Text("Navigator").Wrap(),
                    ]),
                    ctx.VStack(v => [
                        v.Text("┌─ Quad 2 ─┐"),
                        v.Text("Top-Right"),
                        v.Text(""),
                        v.Text("Editor").Wrap(),
                    ]),
                    leftWidth: 20
                ),
                // Bottom row: horizontal splitter
                ctx.HSplitter(
                    ctx.VStack(v => [
                        v.Text("┌─ Quad 3 ─┐"),
                        v.Text("Bottom-Left"),
                        v.Text(""),
                        v.Text("Terminal").Wrap(),
                    ]),
                    ctx.VStack(v => [
                        v.Text("┌─ Quad 4 ─┐"),
                        v.Text("Bottom-Right"),
                        v.Text(""),
                        v.Text("Output").Wrap(),
                    ]),
                    leftWidth: 20
                ),
                topHeight: 8
            )
        ).Title("Quad Split (4 panes)");
    }
}
