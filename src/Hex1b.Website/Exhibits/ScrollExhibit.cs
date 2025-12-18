using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// An exhibit demonstrating vertical and horizontal scrolling.
/// </summary>
public class ScrollExhibit(ILogger<ScrollExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<ScrollExhibit> _logger = logger;

    public override string Id => "scroll";
    public override string Title => "Scroll";
    public override string Description => "Vertical and horizontal scrolling with scrollbar indicators.";

    /// <summary>
    /// State for the scroll exhibit.
    /// </summary>
    private class ScrollExhibitState
    {
        private static readonly string[] ExampleIds = ["vertical", "horizontal", "large-content", "with-buttons", "no-scrollbar", "nested"];
        
        public int SelectedExampleIndex { get; set; } = 0;
        public string SelectedExampleId => ExampleIds[SelectedExampleIndex];
        public ScrollState VerticalScrollState { get; } = new();
        public ScrollState HorizontalScrollState { get; } = new();
        public ScrollState LargeContentScrollState { get; } = new();
        public ScrollState ButtonScrollState { get; } = new();
        
        public IReadOnlyList<string> ExampleItems { get; } =
        [
            "Vertical Scroll",
            "Horizontal Scroll",
            "Large Content",
            "With Buttons",
            "No Scrollbar",
            "Nested in Border",
        ];
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating scroll exhibit widget builder");

        var state = new ScrollExhibitState();

        return () =>
        {
            var ctx = new RootContext();

            var widget = ctx.Splitter(
                ctx.Panel(leftPanel => [
                    leftPanel.VStack(left => [
                        left.Text("Scroll Examples"),
                        left.Text("─────────────────"),
                        left.List(state.ExampleItems, e => state.SelectedExampleIndex = e.SelectedIndex, null),
                        left.Text(""),
                        left.Text("Use ↑↓ to select"),
                        left.Text("Tab to focus scroll"),
                        left.Text("↑↓←→ to scroll"),
                        left.Text("PgUp/PgDn for pages"),
                        left.Text("Home/End for ends"),
                    ])
                ]),
                BuildExampleContent(ctx, state, state.SelectedExampleId),
                leftWidth: 22
            );

            return widget;
        };
    }

    private static Hex1bWidget BuildExampleContent(RootContext ctx, ScrollExhibitState state, string exampleId)
    {
        return exampleId switch
        {
            "vertical" => BuildVerticalExample(ctx, state),
            "horizontal" => BuildHorizontalExample(ctx, state),
            "large-content" => BuildLargeContentExample(ctx, state),
            "with-buttons" => BuildWithButtonsExample(ctx, state),
            "no-scrollbar" => BuildNoScrollbarExample(ctx),
            "nested" => BuildNestedExample(ctx),
            _ => BuildVerticalExample(ctx, state)
        };
    }

    private static Hex1bWidget BuildVerticalExample(RootContext ctx, ScrollExhibitState state)
    {
        return ctx.Border(
            ctx.VScroll(
                v => [
                    v.Text("═══ Vertical Scroll Demo ═══"),
                    v.Text(""),
                    v.Text("This content scrolls vertically."),
                    v.Text("Use the arrow keys ↑↓ when the"),
                    v.Text("scroll widget is focused."),
                    v.Text(""),
                    v.Text("The scrollbar on the right shows"),
                    v.Text("your current position in the"),
                    v.Text("content. The thumb (█) moves as"),
                    v.Text("you scroll through the content."),
                    v.Text(""),
                    v.Text("Try these keys:"),
                    v.Text("• ↑/↓ - Scroll one line"),
                    v.Text("• PgUp/PgDn - Scroll one page"),
                    v.Text("• Home - Jump to start"),
                    v.Text("• End - Jump to end"),
                    v.Text(""),
                    v.Text("The scrollbar adapts its thumb"),
                    v.Text("size based on how much content"),
                    v.Text("is visible vs total content."),
                    v.Text(""),
                    v.Text("You can also use the mouse wheel"),
                    v.Text("to scroll when mouse is enabled."),
                    v.Text(""),
                    v.Text("── End of Content ──"),
                ],
                state.VerticalScrollState
            ),
            title: "Vertical Scroll (↑↓)"
        );
    }

    private static Hex1bWidget BuildHorizontalExample(RootContext ctx, ScrollExhibitState state)
    {
        return ctx.Border(
            ctx.VStack(v => [
                v.Text("═══ Horizontal Scroll Demo ═══"),
                v.Text(""),
                v.Text("Below is horizontally scrollable content:"),
                v.Text(""),
                v.HScroll(
                    h => [
                        h.Text("<<<START>>> | Column 1 | Column 2 | Column 3 | Column 4 | Column 5 | Column 6 | Column 7 | Column 8 | Column 9 | Column 10 | <<<END>>>"),
                    ],
                    state.HorizontalScrollState
                ),
                v.Text(""),
                v.Text("Use ← → arrows when focused to scroll."),
                v.Text("The scrollbar appears at the bottom."),
            ]),
            title: "Horizontal Scroll (←→)"
        );
    }

    private static Hex1bWidget BuildLargeContentExample(RootContext ctx, ScrollExhibitState state)
    {
        return ctx.Border(
            ctx.VScroll(
                v => GenerateLargeContent(v, 100),
                state.LargeContentScrollState
            ),
            title: "Large Content (100 lines)"
        );
    }

    private static Hex1bWidget[] GenerateLargeContent<TParent>(
        WidgetContext<TParent> ctx, 
        int lineCount) where TParent : Hex1bWidget
    {
        var widgets = new List<Hex1bWidget>
        {
            ctx.Text($"═══ {lineCount} Lines of Content ═══"),
            ctx.Text("")
        };
        
        for (int i = 1; i <= lineCount; i++)
        {
            var marker = i switch
            {
                1 => " ← First line",
                25 => " ← Quarter way",
                50 => " ← Halfway point",
                75 => " ← Three quarters",
                100 => " ← Last line",
                _ when i % 10 == 0 => $" ← Line {i}",
                _ => ""
            };
            widgets.Add(ctx.Text($"Line {i:D3}{marker}"));
        }
        
        widgets.Add(ctx.Text(""));
        widgets.Add(ctx.Text("── End of Content ──"));
        
        return widgets.ToArray();
    }

    private static Hex1bWidget BuildWithButtonsExample(RootContext ctx, ScrollExhibitState state)
    {
        return ctx.Border(
            ctx.VScroll(
                v => [
                    v.Text("═══ Scroll with Interactive Content ═══"),
                    v.Text(""),
                    v.Text("This scroll view contains buttons."),
                    v.Text("Tab to navigate between the scroll"),
                    v.Text("widget and the buttons inside."),
                    v.Text(""),
                    v.Button("Button 1 - Near Top", _ => { }),
                    v.Text(""),
                    v.Text("Some content between buttons..."),
                    v.Text("More content..."),
                    v.Text("Even more content..."),
                    v.Text(""),
                    v.Button("Button 2 - Middle", _ => { }),
                    v.Text(""),
                    v.Text("Additional content below..."),
                    v.Text("Keep scrolling..."),
                    v.Text("Almost there..."),
                    v.Text(""),
                    v.Button("Button 3 - Near Bottom", _ => { }),
                    v.Text(""),
                    v.Text("Focus moves: Scroll → Button 1 →"),
                    v.Text("Button 2 → Button 3 → Scroll"),
                    v.Text(""),
                    v.Text("── End of Content ──"),
                ],
                state.ButtonScrollState
            ),
            title: "With Focusable Buttons"
        );
    }

    private static Hex1bWidget BuildNoScrollbarExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VScroll(
                v => [
                    v.Text("═══ Hidden Scrollbar ═══"),
                    v.Text(""),
                    v.Text("This scroll view has the"),
                    v.Text("scrollbar hidden."),
                    v.Text(""),
                    v.Text("You can still scroll with:"),
                    v.Text("• Arrow keys (↑↓)"),
                    v.Text("• Page Up/Down"),
                    v.Text("• Home/End"),
                    v.Text("• Mouse wheel"),
                    v.Text(""),
                    v.Text("But there's no visual indicator"),
                    v.Text("of your position. This can be"),
                    v.Text("useful when you want a cleaner"),
                    v.Text("interface or have other ways"),
                    v.Text("to show scroll position."),
                    v.Text(""),
                    v.Text("Notice how the content area"),
                    v.Text("uses the full width since"),
                    v.Text("there's no scrollbar taking"),
                    v.Text("up space on the right."),
                    v.Text(""),
                    v.Text("── End of Content ──"),
                ],
                showScrollbar: false
            ),
            title: "No Scrollbar (still scrollable)"
        );
    }

    private static Hex1bWidget BuildNestedExample(RootContext ctx)
    {
        return ctx.Border(
            ctx.VStack(v => [
                v.Text("═══ Scroll Nested in Border ═══"),
                v.Text(""),
                v.Border(
                    v.VScroll(
                        inner => [
                            inner.Text("This is scrollable content"),
                            inner.Text("inside a nested border."),
                            inner.Text(""),
                            inner.Text("Nesting works correctly:"),
                            inner.Text("• Clipping is applied"),
                            inner.Text("• Focus navigation works"),
                            inner.Text("• Scrollbar renders properly"),
                            inner.Text(""),
                            inner.Text("You can have multiple"),
                            inner.Text("scroll views on screen"),
                            inner.Text("each with independent"),
                            inner.Text("scroll positions."),
                            inner.Text(""),
                            inner.Text("── End ──"),
                        ]
                    ),
                    "Inner Scroll"
                ),
                v.Text(""),
                v.Text("Content below the nested scroll."),
            ]),
            title: "Nested Scroll in Border"
        );
    }
}
