using Hex1b;
using Hex1b.Input;

// Demo state
var isLeftDrawerExpanded = true;
var isRightDrawerExpanded = false;
var isTopDrawerExpanded = false;
var isBottomDrawerExpanded = false;
var lastAction = "None";
var navItems = new[] { "Home", "Dashboard", "Settings", "Profile", "Help" };
var selectedNavIndex = 0;

// Create the diagnostic terminal for the console drawer
using var cts = new CancellationTokenSource();
var consoleTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(80, 10)
    .WithDiagnosticShell()
    .WithTerminalWidget(out var consoleHandle)
    .Build();

// Start the console terminal in the background
_ = Task.Run(async () =>
{
    try { await consoleTerminal.RunAsync(cts.Token); }
    catch (OperationCanceledException) { }
});

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    ctx.VStack(outer => [
        // TOP DRAWER (first in outer VStack - expands Down, invisible when collapsed)
        outer.Drawer()
            .Expanded(isTopDrawerExpanded)
            // No collapsed content - invisible when collapsed
            .ExpandedContent(e => [
                e.Border(
                    e.VStack(console => [
                        console.HStack(toolbar => [
                            toolbar.Text(" Diagnostic Console"),
                            toolbar.Text("").Fill(),
                            toolbar.Button("▲ Close").OnClick(_ => { 
                                isTopDrawerExpanded = false; 
                                lastAction = "Console closed"; 
                            })
                        ]).FixedHeight(1),
                        console.Terminal(consoleHandle).Fill()
                    ]),
                    title: "Console (Ctrl+`)"
                ).FixedHeight(12)
            ]),
        
        // Main content area with left/right drawers
        outer.HStack(content => [
            // LEFT DRAWER (first in HStack - expands Right)
            content.Drawer()
                .Expanded(isLeftDrawerExpanded)
                .CollapsedContent(c => [
                    c.Button("»").OnClick(_ => { 
                        isLeftDrawerExpanded = true; 
                        lastAction = "Left drawer expanded"; 
                    })
                ])
                .ExpandedContent(e => [
                    e.HStack(toolbar => [
                        toolbar.Text(" Navigation"),
                        toolbar.Text("").Fill(),
                        toolbar.Button("«").OnClick(_ => { 
                            isLeftDrawerExpanded = false; 
                            lastAction = "Left drawer collapsed"; 
                        })
                    ]).FixedHeight(1),
                    e.Text("─────────────────"),
                    ..navItems.Select((item, i) => 
                        e.Button(selectedNavIndex == i ? $"▸ {item}" : $"  {item}")
                            .OnClick(_ => { 
                                selectedNavIndex = i; 
                                lastAction = $"Selected: {item}"; 
                            })
                    )
                ]),
            
            // MAIN CONTENT
            content.Border(
                content.VStack(main => [
                    main.Text(""),
                    main.Text("  Drawer Direction Demo"),
                    main.Text(""),
                    main.Text($"  Left:   {(isLeftDrawerExpanded ? "Expanded →" : "Collapsed")}"),
                    main.Text($"  Right:  {(isRightDrawerExpanded ? "← Expanded" : "Collapsed")}"),
                    main.Text($"  Top:    {(isTopDrawerExpanded ? "Expanded ↓ (Console)" : "Hidden (Ctrl+`)")}"),
                    main.Text($"  Bottom: {(isBottomDrawerExpanded ? "↑ Expanded" : "Collapsed")}"),
                    main.Text(""),
                    main.Text($"  Last Action: {lastAction}"),
                    main.Text(""),
                    main.HStack(buttons => [
                        buttons.Text("  "),
                        buttons.Button("Toggle Left").OnClick(_ => {
                            isLeftDrawerExpanded = !isLeftDrawerExpanded;
                            lastAction = isLeftDrawerExpanded ? "Left expanded" : "Left collapsed";
                        }),
                        buttons.Text(" "),
                        buttons.Button("Toggle Right").OnClick(_ => {
                            isRightDrawerExpanded = !isRightDrawerExpanded;
                            lastAction = isRightDrawerExpanded ? "Right expanded" : "Right collapsed";
                        })
                    ]).FixedHeight(1),
                    main.HStack(buttons => [
                        buttons.Text("  "),
                        buttons.Button("Exit").OnClick(e => e.Context.RequestStop())
                    ]).FixedHeight(1)
                ]),
                title: "Content"
            ).Fill(),
            
            // RIGHT DRAWER (last in HStack - expands Left)
            content.Drawer()
                .Expanded(isRightDrawerExpanded)
                .CollapsedContent(c => [
                    c.Button("«").OnClick(_ => { 
                        isRightDrawerExpanded = true; 
                        lastAction = "Right drawer expanded"; 
                    })
                ])
                .ExpandedContent(e => [
                    e.HStack(toolbar => [
                        toolbar.Button("»").OnClick(_ => { 
                            isRightDrawerExpanded = false; 
                            lastAction = "Right drawer collapsed"; 
                        }),
                        toolbar.Text("").Fill(),
                        toolbar.Text("Details ")
                    ]).FixedHeight(1),
                    e.Text("─────────────────"),
                    e.Text($" {navItems[selectedNavIndex]}"),
                    e.Text(" ───────────"),
                    e.Text(" Details here")
                ])
        ]).Fill(),
        
        // BOTTOM DRAWER (last in outer VStack - expands Up)
        outer.Drawer()
            .Expanded(isBottomDrawerExpanded)
            .CollapsedContent(c => [
                c.HStack(bar => [
                    bar.Button("▲ Output").OnClick(_ => { 
                        isBottomDrawerExpanded = true; 
                        lastAction = "Bottom drawer expanded"; 
                    })
                ]).FixedHeight(1)
            ])
            .ExpandedContent(e => [
                e.Border(
                    e.VStack(output => [
                        output.HStack(toolbar => [
                            toolbar.Text(" Output"),
                            toolbar.Text("").Fill(),
                            toolbar.Button("▼ Close").OnClick(_ => { 
                                isBottomDrawerExpanded = false; 
                                lastAction = "Bottom drawer collapsed"; 
                            })
                        ]).FixedHeight(1),
                        output.Text("─────────────────────────────────────────────"),
                        output.Text(" Build output:"),
                        output.Text(" [INFO] Building project..."),
                        output.Text(" [INFO] Compilation successful"),
                        output.Text(" [INFO] 0 warnings, 0 errors")
                    ]),
                    title: "Output"
                ).FixedHeight(10)
            ]),
        
        // Status bar
        outer.InfoBar([
            "Tab", "Navigate",
            "F12", "Console",
            "Ctrl+C", "Exit"
        ])
    ])
    // Global key binding for F12 to toggle console (Ctrl+` doesn't work reliably in terminals)
    .WithInputBindings(bindings => {
        bindings.Key(Hex1bKey.F12).Global().Action(_ => {
            isTopDrawerExpanded = !isTopDrawerExpanded;
            lastAction = isTopDrawerExpanded ? "Console opened" : "Console closed";
        }, "Toggle console");
    }))
    .WithMouse()
    .WithRenderOptimization()
    .Build();

try
{
    await terminal.RunAsync();
}
finally
{
    cts.Cancel();
    await consoleTerminal.DisposeAsync();
}
