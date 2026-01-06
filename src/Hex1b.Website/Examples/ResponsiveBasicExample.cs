using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// An elaborate responsive example with navigation panel, splitter, and adaptive content layout.
/// Demonstrates how panels reorganize from horizontal to vertical layout as terminal width changes.
/// </summary>
public class ResponsiveBasicExample(ILogger<ResponsiveBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ResponsiveBasicExample> _logger = logger;

    public override string Id => "responsive-basic";
    public override string Title => "Responsive Layout Demo";
    public override string Description => "Adaptive layout with navigation and content panels that reorganize based on terminal width.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating responsive elaborate example");

        return () =>
        {
            var ctx = new RootContext();
            
            // Navigation panel (always visible on left side in wide layouts)
            var navPanel = ctx.ThemePanel(theme => theme
                .Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
                .Set(BorderTheme.TitleColor, Hex1bColor.White), 
                t => [
                    t.Border(b => [
                        b.Text("📋 Navigation"),
                        b.Text(""),
                        b.Text("• Dashboard"),
                        b.Text("• Analytics"),
                        b.Text("• Settings"),
                        b.Text("• Profile"),
                        b.Text(""),
                        b.Text("Press Tab to"),
                        b.Text("navigate items")
                    ], title: "Menu")
                ]);
            
            // Primary content panel (high priority - always visible)
            var primaryPanel = ctx.ThemePanel(theme => theme
                .Set(BorderTheme.BorderColor, Hex1bColor.Green)
                .Set(BorderTheme.TitleColor, Hex1bColor.White),
                t => [
                    t.Border(b => [
                        b.Text("📊 Primary Content"),
                        b.Text(""),
                        b.Text("Main dashboard view"),
                        b.Text(""),
                        b.Text("💚 Breakpoint: >= 100"),
                        b.Text(""),
                        b.Text("This panel has the"),
                        b.Text("highest priority and"),
                        b.Text("is always visible.")
                    ], title: "Dashboard")
                ]);
            
            // Secondary content panel (medium priority - visible when wide enough)
            var secondaryPanel = ctx.ThemePanel(theme => theme
                .Set(BorderTheme.BorderColor, Hex1bColor.Yellow)
                .Set(BorderTheme.TitleColor, Hex1bColor.Black),
                t => [
                    t.Border(b => [
                        b.Text("📈 Secondary Content"),
                        b.Text(""),
                        b.Text("Analytics & Stats"),
                        b.Text(""),
                        b.Text("💛 Breakpoint: >= 120"),
                        b.Text(""),
                        b.Text("This panel appears"),
                        b.Text("alongside primary"),
                        b.Text("when width >= 120.")
                    ], title: "Analytics")
                ]);
            
            return ctx.Responsive(r => [
                // Extra Wide (>= 120): Nav | Primary + Secondary side-by-side
                r.WhenMinWidth(120, r =>
                    r.HSplitter(
                        navPanel,
                        r.HStack(h => [
                            h.Layout(primaryPanel).FillWidth(3),
                            h.Layout(secondaryPanel).FillWidth(2)
                        ]),
                        leftWidth: 25
                    )
                ),
                
                // Wide (>= 100): Nav | Primary + Secondary stacked
                r.WhenMinWidth(100, r =>
                    r.HSplitter(
                        navPanel,
                        r.VStack(v => [
                            v.Layout(primaryPanel).FillHeight(3),
                            v.Layout(secondaryPanel).FillHeight(2)
                        ]),
                        leftWidth: 25
                    )
                ),
                
                // Medium (>= 80): Just Nav and Primary in splitter
                r.WhenMinWidth(80, r =>
                    r.HSplitter(navPanel, primaryPanel, leftWidth: 25)
                ),
                
                // Narrow (< 80): All panels stacked vertically
                r.Otherwise(r =>
                    r.VStack(v => [
                        v.Layout(navPanel).FixedHeight(10),
                        v.Layout(primaryPanel).FillHeight()
                    ])
                )
            ]);
        };
    }
}
