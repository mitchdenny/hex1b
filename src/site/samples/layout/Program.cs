using Hex1b;
using Hex1b.Widgets;


var sample = new LayoutExample();
var builder = sample.CreateWidgetBuilder();
await using var app = new Hex1bApp(_ => builder());
await app.RunAsync();

/// <summary>
/// An example for exploring clipping and wrapping behavior of child widgets
/// when there is not enough horizontal or vertical space.
/// </summary>
internal class LayoutExample
{

    public string Id => "layout";
    public string Title => "Layout";
    public string Description => "Explore clipping and wrapping behavior when space is constrained.";

    /// <summary>
    /// State for the layout example.
    /// </summary>
    private class LayoutState
    {
        private static readonly string[] ExampleIds = ["text-wrapping", "text-clipping", "text-ellipsis", "nested-layout", "border-clipping"];
        
        public int SelectedExampleIndex { get; set; } = 0;
        public string SelectedExampleId => ExampleIds[SelectedExampleIndex];
        
        public IReadOnlyList<string> ExampleItems { get; } =
        [
            "Text Wrapping",
            "Text Clipping",
            "Text Ellipsis",
            "Nested Layouts",
            "Border Clipping",
        ];
    }

    public Func<Hex1bWidget> CreateWidgetBuilder()
    {

        var state = new LayoutState();

        return () =>
        {
            var ctx = new RootContext();

            var widget = ctx.HSplitter(
                ctx.Layout(
                    ctx.VStack(left => [
                        left.Text("Layout Examples"),
                        left.Text("───────────────────"),
                        left.List(state.ExampleItems).OnSelectionChanged(e => state.SelectedExampleIndex = e.SelectedIndex),
                        left.Text(""),
                        left.Text("Use ↑↓ to navigate"),
                    ]),
                    ClipMode.Clip
                ),
                ctx.Layout(
                    BuildExampleContent(ctx, state.SelectedExampleId),
                    ClipMode.Clip
                ),
                leftWidth: 22
            );

            return widget;
        };
    }

    private static Hex1bWidget BuildExampleContent(RootContext ctx, string exampleId)
    {
        return exampleId switch
        {
            "text-wrapping" => BuildTextWrappingExample(ctx),
            "text-clipping" => BuildTextClippingExample(ctx),
            "text-ellipsis" => BuildTextEllipsisExample(ctx),
            "nested-layout" => BuildNestedLayoutExample(ctx),
            "border-clipping" => BuildBorderClippingExample(ctx),
            _ => BuildTextWrappingExample(ctx)
        };
    }

    private static Hex1bWidget BuildTextWrappingExample(RootContext ctx)
    {
        var loremIpsum = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";
        var explanation = "TextOverflow.Wrap automatically breaks text at word boundaries when it exceeds the available width.";

        return ctx.VStack(v => [
            v.Text("═══ Text Wrapping Demo ═══"),
            v.Text(""),
            v.Text(explanation).Wrap(),
            v.Text(""),
            v.Text("─── Long Paragraph ───"),
            v.Text(""),
            v.Text(loremIpsum).Wrap(),
            v.Text(""),
            v.Text("─── End of Demo ───"),
        ]);
    }

    private static Hex1bWidget BuildTextClippingExample(RootContext ctx)
    {
        // Intentionally very wide so it overflows even on large terminals (e.g. 160x50).
        const int innerWidth = 240;

        static string MakeTopBottom(char left, char fill, char right)
            => $"{left}{new string(fill, innerWidth)}{right}";

        static string MakeContent(string content)
        {
            // Ensure each content line is exactly innerWidth chars (truncate or pad).
            var normalized = content.Length > innerWidth ? content[..innerWidth] : content.PadRight(innerWidth);
            return $"║{normalized}║";
        }

        var topLine = MakeTopBottom('╔', '═', '╗');
        var contentLine1 = MakeContent("  TECHNICAL SPECIFICATIONS - SYSTEM ARCHITECTURE OVERVIEW - VERSION 2.4.1");
        var contentLine2 = MakeContent("  Component: Terminal Rendering Engine | Status: Active | Memory: 256MB | Threads: 4");
        var contentLine3 = MakeContent("  Rendering Pipeline: Widget Tree → Reconciliation → Measure → Arrange → Render");
        var contentLine4 = MakeContent("  Notes: This line is intentionally padded to force right-edge clipping in the demo.");
        var bottomLine = MakeTopBottom('╚', '═', '╝');

        return ctx.VStack(v => [
            v.Text("═══ Text Clipping Demo ═══"),
            v.Text(""),
            v.Text("TextOverflow.Overflow (default) allows").Wrap(),
            v.Text("text to extend beyond bounds. The").Wrap(),
            v.Text("LayoutNode clips it at render time.").Wrap(),
            v.Text(""),
            v.Text("─── Wide ASCII Art (clipped) ───"),
            v.Text(""),
            v.Text(topLine),
            v.Text(contentLine1),
            v.Text(contentLine2),
            v.Text(contentLine3),
            v.Text(contentLine4),
            v.Text(bottomLine),
            v.Text(""),
            v.Text("Notice how the box is cut off at the").Wrap(),
            v.Text("right edge of this panel.").Wrap(),
        ]);
    }

    private static Hex1bWidget BuildTextEllipsisExample(RootContext ctx)
    {
        var longTitle = "This is an extremely long title that should be truncated with ellipsis";
        var longDescription = "A very detailed description that goes on and on explaining every little detail about this item";

        return ctx.VStack(v => [
            v.Text("═══ Text Ellipsis Demo ═══"),
            v.Text(""),
            v.Text("TextOverflow.Ellipsis truncates text").Wrap(),
            v.Text("and adds '...' at the end.").Wrap(),
            v.Text(""),
            v.Text("─── File List Example ───"),
            v.Text(""),
            v.Text("📁 Documents/").Ellipsis(),
            v.Text("  📄 " + longTitle).Ellipsis(),
            v.Text("  📄 Another file with a really long name here").Ellipsis(),
            v.Text("  📄 Short.txt").Ellipsis(),
            v.Text(""),
            v.Text("─── Card Example ───"),
            v.Text(""),
            v.Text("┌─────────────────────────────────────┐"),
            v.Text("│ Title: " + longTitle).Ellipsis(),
            v.Text("│ Desc:  " + longDescription).Ellipsis(),
            v.Text("└─────────────────────────────────────┘"),
        ]);
    }

    private static Hex1bWidget BuildNestedLayoutExample(RootContext ctx)
    {
        var innerText = "This text is inside a nested layout region with its own clipping boundary.";

        return ctx.VStack(v => [
            v.Text("═══ Nested Layouts Demo ═══"),
            v.Text(""),
            v.Text("Layout regions can be nested. Each").Wrap(),
            v.Text("LayoutNode establishes its own clip").Wrap(),
            v.Text("boundary for descendants.").Wrap(),
            v.Text(""),
            v.Text("─── Outer Region ───"),
            v.Text(""),
            v.Text("Content in outer region spans the full width of this panel area.").Wrap(),
            v.Text(""),
            v.Border(border => [
                border.Text("Inner bordered region:"),
                border.Text(innerText).Wrap(),
                border.Text("More nested content here that should wrap nicely within the border.").Wrap(),
            ]).Title("Nested"),
            v.Text(""),
            v.Text("Content after the nested region.").Wrap(),
        ]);
    }

    private static Hex1bWidget BuildBorderClippingExample(RootContext ctx)
    {
        var wideContent = "This line of text is intentionally very wide to demonstrate how borders handle overflow content when there isn't enough horizontal space.";

        return ctx.VStack(v => [
            v.Text("═══ Border Clipping Demo ═══"),
            v.Text(""),
            v.Text("Borders contain child content and").Wrap(),
            v.Text("should clip properly.").Wrap(),
            v.Text(""),
            v.Border(border => [
                border.Text("Normal content inside"),
                border.Text("the border widget."),
            ]).Title("Simple"),
            v.Text(""),
            v.Border(border => [
                border.Text("Wide content that overflows:"),
                border.Text(wideContent),
                border.Text("═══════════════════════════════════════════════════════════════════"),
            ]).Title("Overflow"),
            v.Text(""),
            v.Border(border => [
                border.Text("Wrapped content inside:").Wrap(),
                border.Text(wideContent).Wrap(),
            ]).Title("Wrapped"),
        ]);
    }
}
