using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// Demo state
var isLeftDrawerExpanded = true;
var isRightDrawerExpanded = false;
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
    // Wrap in ZStack for popup support (required for overlay mode)
    ctx.ZStack(z => [
        z.VStack(outer => [
            // TOP DRAWER (first in outer VStack - expands Down, OVERLAY mode)
            outer.Drawer()
                .AsOverlay()  // <-- Overlay mode: floats above content
                .CollapsedContent(c => [
                    // When in overlay mode, clicking anywhere on this triggers the popup
                    c.HStack(bar => [
                        bar.Text(" ▼ Console (click to open)")
                    ]).FixedHeight(1)
                ])
                .ExpandedContent(e => [
                    e.Border(
                        e.VStack(console => [
                            console.HStack(toolbar => [
                                toolbar.Text(" Diagnostic Console"),
                                toolbar.Text("").Fill(),
                                toolbar.Button("▲ Close").OnClick(ctx => { 
                                    lastAction = "Console closed"; 
                                    ctx.Popups.Pop();  // Dismiss the overlay popup
                                })
                            ]).FixedHeight(1),
                            console.Terminal(consoleHandle).Fill()
                        ]),
                        title: "Console (Click outside to close)"
                    ).FixedHeight(12)
                ])
                .OnCollapsed(() => { lastAction = "Console closed (click-away)"; }),
            
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
                        main.Text($"  Top:    Overlay mode (click to open)"),
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
            
            // BOTTOM DRAWER (last in outer VStack - expands Up, inline mode)
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
                                output.Text(" Output"),
                                output.Text("").Fill(),
                                output.Button("▼ Close").OnClick(_ => { 
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
                "Click Console", "Open Overlay",
                "Ctrl+C", "Exit"
            ])
        ])
    ])
    // No global key bindings needed for drawer - click triggers overlay
    .WithInputBindings(bindings => { }))
    .WithMouse()
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
