using Hex1b;
using Hex1b.Theming;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx =>
    {
        var navPanel = ctx.ThemePanel(theme => theme
            .Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
            t => [
                t.Border(b => [
                    b.Text("📋 Navigation"),
                    b.Text("• Dashboard"),
                    b.Text("• Settings")
                ]).Title("Menu")
            ]);
        
        var primaryPanel = ctx.ThemePanel(theme => theme
            .Set(BorderTheme.BorderColor, Hex1bColor.Green),
            t => [
                t.Border(b => [
                    b.Text("📊 Primary Content"),
                    b.Text("Main view - always visible"),
                    b.Text("💚 Breakpoint: >= 100")
                ]).Title("Dashboard")
            ]);
        
        var secondaryPanel = ctx.ThemePanel(theme => theme
            .Set(BorderTheme.BorderColor, Hex1bColor.Yellow),
            t => [
                t.Border(b => [
                    b.Text("📈 Secondary Content"),
                    b.Text("Visible when width >= 120"),
                    b.Text("💛 Breakpoint: >= 120")
                ]).Title("Analytics")
            ]);
        
        return ctx.Responsive(r => [
            // Extra Wide: Nav | Primary + Secondary side-by-side
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
            
            // Wide: Nav | Primary + Secondary stacked
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
            
            // Medium: Nav | Primary only
            r.WhenMinWidth(80, r =>
                r.HSplitter(navPanel, primaryPanel, leftWidth: 25)
            ),
            
            // Narrow: All stacked
            r.Otherwise(r =>
                r.VStack(v => [
                    v.Layout(navPanel).FixedHeight(10),
                    v.Layout(primaryPanel).FillHeight()
                ])
            )
        ]);
    })
    .Build();

await terminal.RunAsync();
