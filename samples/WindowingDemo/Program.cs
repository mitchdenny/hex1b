using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using WindowingDemo;

// Demo state
var windowCounter = 0;
var openWindowCount = 0;
var statusMessage = "Ready";

// Track custom action feedback per window
var windowActionFeedback = new Dictionary<string, string>();

// Track terminal instances for cleanup
var terminalInstances = new Dictionary<string, (Hex1bTerminal Terminal, CancellationTokenSource Cts)>();

// Surface demo state (shared across windows of same type)
var fireflies = FirefliesDemo.CreateFireflies();
var demoRandom = new Random();

// Sample table data
var tableData = new List<Employee>
{
    new("Alice Johnson", "Engineer", 32, "Active"),
    new("Bob Smith", "Designer", 28, "Active"),
    new("Carol Williams", "Manager", 45, "On Leave"),
    new("David Brown", "Developer", 35, "Active"),
    new("Eve Davis", "Analyst", 29, "Active"),
    new("Frank Miller", "Engineer", 41, "Inactive"),
    new("Grace Wilson", "Designer", 33, "Active"),
    new("Henry Taylor", "Developer", 27, "Active"),
    new("Iris Anderson", "Manager", 52, "Active"),
    new("Jack Thomas", "Analyst", 31, "On Leave"),
};

// Table option toggles
var tableCompactMode = true;
var tableFillWidth = true;
var tableShowSelection = false;
object? tableFocusedKey = tableData[0].Name;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMcpDiagnostics("WindowingDemo", forceEnable: true)
    .WithHex1bApp((app, options) => ctx =>
    {
        // Update fireflies each frame
        FirefliesDemo.Update(fireflies, demoRandom);
        
        return ctx.VStack(outer => [
            outer.NotificationPanel(outer.VStack(main => [
            // ─────────────────────────────────────────────────────────────────
            // MENU BAR
            // ─────────────────────────────────────────────────────────────────
            main.MenuBar(m => [
                m.Menu("File", m => [
                    m.MenuItem("New Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"window-{num}",
                            $"Window {num}",
                            w => BuildWindowContent(w, num),
                            new WindowOptions
                            {
                                Width = 45,
                                Height = 12,
                                Position = new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 3, OffsetY: (num - 1) * 2),
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Window {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Window {num}";
                    }),
                    m.MenuItem("New Window with Custom Actions").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        var windowId = $"custom-actions-{num}";
                        windowActionFeedback[windowId] = "";
                        e.Windows.Open(
                            windowId,
                            $"Custom Actions {num}",
                            w => BuildCustomActionsContent(w, num, windowActionFeedback.GetValueOrDefault(windowId, "")),
                            new WindowOptions
                            {
                                Width = 50,
                                Height = 12,
                                Position = new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 3, OffsetY: (num - 1) * 2),
                                LeftTitleBarActions = [
                                    new WindowAction("📌", ctx => { 
                                        windowActionFeedback[windowId] = "📌 Window pinned!";
                                        statusMessage = $"Pinned Window {num}"; 
                                    }),
                                    new WindowAction("📋", ctx => { 
                                        windowActionFeedback[windowId] = "📋 Content copied!";
                                        statusMessage = $"Copied from Window {num}"; 
                                    })
                                ],
                                RightTitleBarActions = [
                                    new WindowAction("?", ctx => { 
                                        windowActionFeedback[windowId] = "❓ Help requested!";
                                        statusMessage = $"Help for Window {num}"; 
                                    }),
                                    WindowAction.Close()
                                ],
                                OnClose = () => { 
                                    openWindowCount--; 
                                    windowActionFeedback.Remove(windowId);
                                    statusMessage = $"Closed: Custom Actions {num}"; 
                                }
                            }
                        );
                        statusMessage = $"Opened: Custom Actions {num}";
                    }),
                    m.MenuItem("New Terminal").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        var windowId = $"terminal-{num}";
                        
                        // Create terminal with PTY process
                        var cts = new CancellationTokenSource();
                        var bashTerminal = Hex1bTerminal.CreateBuilder()
                            .WithPtyProcess("bash")
                            .WithTerminalWidget(out var bashHandle)
                            .Build();
                        
                        // Track for cleanup
                        terminalInstances[windowId] = (bashTerminal, cts);
                        
                        // Start the terminal in the background
                        _ = bashTerminal.RunAsync(cts.Token);
                        
                        e.Windows.Open(
                            windowId,
                            $"Terminal {num}",
                            _ => new TerminalWidget(bashHandle),
                            new WindowOptions
                            {
                                Width = 80,
                                Height = 24,
                                Position = new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 2, OffsetY: (num - 1)),
                                IsResizable = true,
                                MinWidth = 40,
                                MinHeight = 12,
                                OnClose = () => {
                                    openWindowCount--;
                                    statusMessage = $"Closed: Terminal {num}";
                                    // Clean up terminal
                                    if (terminalInstances.TryGetValue(windowId, out var instance))
                                    {
                                        instance.Cts.Cancel();
                                        instance.Terminal.Dispose();
                                        terminalInstances.Remove(windowId);
                                    }
                                }
                            }
                        );
                        statusMessage = $"Opened: Terminal {num}";
                    }),
                    m.MenuItem("New Full Chrome Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"full-{num}",
                            $"Full Chrome {num}",
                            w => BuildWindowContent(w, num),
                            new WindowOptions
                            {
                                Width = 45,
                                Height = 12,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Window {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Full Chrome {num}";
                    }),
                    m.MenuItem("New Frameless Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"frame-{num}",
                            $"Frameless {num}",
                            w => BuildFramelessContent(w, num),
                            new WindowOptions
                            {
                                Width = 35,
                                Height = 8,
                                ShowTitleBar = false,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Frameless {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Frameless {num}";
                    }),
                    m.MenuItem("New Resizable Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"resizable-{num}",
                            $"Resizable {num}",
                            w => BuildResizableContent(w, num),
                            new WindowOptions
                            {
                                Width = 50,
                                Height = 15,
                                IsResizable = true,
                                MinWidth = 30,
                                MinHeight = 10,
                                MaxWidth = 80,
                                MaxHeight = 30,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Resizable {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Resizable {num}";
                    }),
                    m.MenuItem("New Table Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"table-{num}",
                            $"Employee Table {num}",
                            w => BuildTableContent(w, tableData, tableCompactMode, tableFillWidth, tableShowSelection,
                                tableFocusedKey, key => tableFocusedKey = key,
                                isCompact => tableCompactMode = isCompact,
                                isFill => tableFillWidth = isFill,
                                showSel => tableShowSelection = showSel),
                            new WindowOptions
                            {
                                Width = 65,
                                Height = 18,
                                IsResizable = true,
                                MinWidth = 50,
                                MinHeight = 10,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Table {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Table {num}";
                    }),
                    m.Separator(),
                    m.MenuItem("Close All Windows").OnActivated(e => {
                        e.Windows.CloseAll();
                        openWindowCount = 0;
                        // Clean up all terminal instances
                        foreach (var (_, instance) in terminalInstances)
                        {
                            instance.Cts.Cancel();
                            instance.Terminal.Dispose();
                        }
                        terminalInstances.Clear();
                        statusMessage = "Closed all windows";
                    }),
                    m.Separator(),
                    m.MenuItem("Exit").OnActivated(e => e.Context.RequestStop())
                ]),
                m.Menu("Window", m => [
                    m.MenuItem("Open Modal Dialog").OnActivated(e => {
                        openWindowCount++;
                        e.Windows.Open(
                            "modal",
                            "Modal Dialog",
                            w => w.VStack(v => [
                                v.Text(""),
                                v.Text("  ⚠️  This is a modal dialog!"),
                                v.Text(""),
                                v.Text("  Background windows are blocked."),
                                v.Text(""),
                                v.HStack(h => [
                                    h.Text("  "),
                                    h.Button("OK").OnClick(ev => {
                                        ev.Context.Windows.Get("modal")?.CloseWithResult(true);
                                    }),
                                    h.Text(" "),
                                    h.Button("Cancel").OnClick(ev => {
                                        ev.Context.Windows.Get("modal")?.CloseWithResult(false);
                                    })
                                ])
                            ]),
                            new WindowOptions
                            {
                                Width = 50,
                                Height = 10,
                                IsModal = true,
                                OnClose = () => { openWindowCount--; statusMessage = "Modal closed"; }
                            }
                        );
                        statusMessage = "Opened modal dialog";
                    }),
                    m.MenuItem("Confirm Delete").OnActivated(e => {
                        openWindowCount++;
                        e.Windows.Open(
                            "confirm-delete",
                            "Confirm Delete",
                            w => w.VStack(v => [
                                v.Text(""),
                                v.Text("  🗑️  Delete all items?"),
                                v.Text(""),
                                v.Text("  This action cannot be undone."),
                                v.Text(""),
                                v.HStack(h => [
                                    h.Text("  "),
                                    h.Button("Delete").OnClick(ev => {
                                        statusMessage = "Delete confirmed!";
                                        ev.Context.Windows.Get("confirm-delete")?.Close();
                                    }),
                                    h.Text(" "),
                                    h.Button("Cancel").OnClick(ev => {
                                        statusMessage = "Delete cancelled";
                                        ev.Context.Windows.Get("confirm-delete")?.Close();
                                    })
                                ])
                            ]),
                            new WindowOptions
                            {
                                Width = 45,
                                Height = 10,
                                IsModal = true,
                                OnClose = () => openWindowCount--
                            }
                        );
                        statusMessage = "Confirm before deleting";
                    }),
                    m.Separator(),
                    m.MenuItem("Show Notification").OnActivated(e => {
                        e.Context.Notifications.Post(
                            new Notification("New Data Available", "Employee records have been updated.")
                                .Timeout(TimeSpan.FromSeconds(10))
                                .PrimaryAction("View Table", async ctx => {
                                    windowCounter++;
                                    openWindowCount++;
                                    var num = windowCounter;
                                    ctx.InputTrigger.Windows.Open(
                                        $"table-notif-{num}",
                                        $"Employee Table {num}",
                                        w => BuildTableContent(w, tableData, tableCompactMode, tableFillWidth, tableShowSelection,
                                            tableFocusedKey, key => tableFocusedKey = key,
                                            isCompact => tableCompactMode = isCompact,
                                            isFill => tableFillWidth = isFill,
                                            showSel => tableShowSelection = showSel),
                                        new WindowOptions
                                        {
                                            Width = 65,
                                            Height = 18,
                                            IsResizable = true,
                                            MinWidth = 50,
                                            MinHeight = 10,
                                            OnClose = () => { openWindowCount--; statusMessage = $"Closed: Table {num}"; }
                                        }
                                    );
                                    statusMessage = $"Opened table from notification";
                                    ctx.Dismiss();
                                })
                        );
                        statusMessage = "Notification posted";
                    }),
                    m.Separator(),
                    m.MenuItem("Tile Windows").OnActivated(e => statusMessage = "Tile not yet implemented"),
                    m.MenuItem("Cascade Windows").OnActivated(e => statusMessage = "Cascade not yet implemented")
                ]),
                m.Menu("Samples", m => [
                    m.MenuItem("Fireflies").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"fireflies-{num}",
                            $"Fireflies {num}",
                            _ => new SurfaceWidget(s => FirefliesDemo.BuildLayers(s, fireflies))
                                .Width(SizeHint.Fixed(FirefliesDemo.WidthCells))
                                .Height(SizeHint.Fixed(FirefliesDemo.HeightCells))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = FirefliesDemo.RequiredWidth,
                                Height = FirefliesDemo.RequiredHeight,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Fireflies {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Fireflies {num}";
                    }),
                    m.MenuItem("Radar").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"radar-{num}",
                            $"Radar {num}",
                            _ => new SurfaceWidget(s => RadarDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Radar {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Radar {num}";
                    }),
                    m.MenuItem("Gravity").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"gravity-{num}",
                            $"Gravity {num}",
                            _ => new SurfaceWidget(s => GravityDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Gravity {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Gravity {num}";
                    }),
                    m.MenuItem("Snow").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"snow-{num}",
                            $"Snow {num}",
                            _ => new SurfaceWidget(s => SnowDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Snow {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Snow {num}";
                    }),
                    m.MenuItem("Noise").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"noise-{num}",
                            $"Noise {num}",
                            _ => new SurfaceWidget(s => NoiseDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Noise {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Noise {num}";
                    }),
                    m.MenuItem("Shadows").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"shadows-{num}",
                            $"Shadows {num}",
                            _ => new SurfaceWidget(s => ShadowDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Shadows {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Shadows {num}";
                    }),
                    m.MenuItem("Fluid").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"fluid-{num}",
                            $"Fluid {num}",
                            _ => new SurfaceWidget(s => FluidDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Fluid {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Fluid {num}";
                    }),
                    m.MenuItem("Smart Matter").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"smartmatter-{num}",
                            $"Smart Matter {num}",
                            _ => new SurfaceWidget(s => SmartMatterDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Smart Matter {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Smart Matter {num}";
                    }),
                    m.MenuItem("Slime Mold").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            $"slimemold-{num}",
                            $"Slime Mold {num}",
                            _ => new SurfaceWidget(s => SlimeMoldDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            new WindowOptions
                            {
                                Width = 62,
                                Height = 22,
                                IsResizable = true,
                                OnClose = () => { openWindowCount--; statusMessage = $"Closed: Slime Mold {num}"; }
                            }
                        );
                        statusMessage = $"Opened: Slime Mold {num}";
                    })
                ]),
                m.Menu("Help", m => [
                    m.MenuItem("About").OnActivated(e => {
                        if (e.Windows.IsOpen("about")) {
                            e.Windows.BringToFront("about");
                            return;
                        }
                        openWindowCount++;
                        e.Windows.Open(
                            "about",
                            "About",
                            w => w.VStack(v => [
                                v.Text(""),
                                v.Text("  Hex1b Floating Windows Demo"),
                                v.Text("  Version: 1.0.0"),
                                v.Text(""),
                                v.Text("  Features:"),
                                v.Text("  • Multiple window styles"),
                                v.Text("  • Drag to move"),
                                v.Text("  • Min/Max/Close buttons"),
                                v.Text(""),
                                v.HStack(h => [
                                    h.Text("  "),
                                    h.Button("Close")
                                ])
                            ]),
                            new WindowOptions
                            {
                                Width = 40,
                                Height = 13,
                                OnClose = () => { openWindowCount--; statusMessage = "About closed"; }
                            }
                        );
                        statusMessage = "Opened About";
                    })
                ])
            ]),

            // ─────────────────────────────────────────────────────────────────
            // WINDOW PANEL (MDI area) - Unbounded allows windows to go outside
            // ─────────────────────────────────────────────────────────────────
            main.WindowPanel(w =>
                w.VStack(center => [
                    center.Text(""),
                    center.Text(""),
                    center.Text(""),
                    center.Text("╔═══════════════════════════════════════╗"),
                    center.Text("║      Floating Windows Demo            ║"),
                    center.Text("║                                       ║"),
                    center.Text("║  Use File menu to open windows        ║"),
                    center.Text("║  Drag title bar to move               ║"),
                    center.Text("║  Windows can be dragged off-screen    ║"),
                    center.Text("╚═══════════════════════════════════════╝")
                ])
            ).Unbounded().Fill(),

            // ─────────────────────────────────────────────────────────────────
            // STATUS BAR
            // ─────────────────────────────────────────────────────────────────
            main.InfoBar([
                "Status", statusMessage,
                "Windows", $"{openWindowCount} open"
            ])
        ])).Fill()
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

// Build window content - now receives context from window
static Hex1bWidget BuildWindowContent(WidgetContext<Hex1bWidget> ctx, int windowNum)
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text($"  This is Window #{windowNum}"),
        v.Text(""),
        v.Text("  Features:"),
        v.Text("  • Drag title bar to move"),
        v.Text("  • Click buttons to interact"),
        v.Text("  • Press Escape to close"),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Button("Action"),
            h.Text(" "),
            h.Button("Close")
        ])
    ]);
}

static Hex1bWidget BuildCustomActionsContent(WidgetContext<Hex1bWidget> ctx, int windowNum, string actionFeedback)
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text($"  Custom Actions Window #{windowNum}"),
        v.Text(""),
        v.Text("  Click the title bar icons:"),
        v.Text("  📌 Pin  📋 Copy  ? Help"),
        v.Text(""),
        string.IsNullOrEmpty(actionFeedback)
            ? v.Text("  (No action yet)")
            : v.Text($"  → {actionFeedback}"),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Button("Close")
        ])
    ]);
}

static Hex1bWidget BuildFramelessContent(WidgetContext<Hex1bWidget> ctx, int windowNum)
{
    return ctx.VStack(v => [
        v.Text($"  Frameless Window #{windowNum}"),
        v.Text(""),
        v.Text("  No title bar - just content."),
        v.Text("  Press Escape to close."),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Button("Close")
        ])
    ]);
}

static Hex1bWidget BuildResizableContent(WidgetContext<Hex1bWidget> ctx, int windowNum)
{
    return ctx.VStack(v => [
        v.Text(""),
        v.Text($"  Resizable Window #{windowNum}"),
        v.Text(""),
        v.Text("  🖱️  Resize Handles:"),
        v.Text("  • Drag left/right edges"),
        v.Text("  • Drag bottom edge"),
        v.Text("  • Drag corners (◢)"),
        v.Text(""),
        v.Text("  📏 Constraints:"),
        v.Text("  • Min: 30×10"),
        v.Text("  • Max: 80×30"),
        v.Text(""),
        v.HStack(h => [
            h.Text("  "),
            h.Button("Close")
        ])
    ]).Fill();
}

static Hex1bWidget BuildTableContent(
    WidgetContext<Hex1bWidget> ctx,
    IReadOnlyList<Employee> data,
    bool isCompact,
    bool isFillWidth,
    bool showSelection,
    object? focusedKey,
    Action<object?> onFocusChanged,
    Action<bool> onCompactChanged,
    Action<bool> onFillWidthChanged,
    Action<bool> onSelectionChanged)
{
    
    var table = ctx.Table(data)
        .RowKey(e => e.Name)
        .Header(h => [
            h.Cell("Name").Width(SizeHint.Fill),
            h.Cell("Role").Width(SizeHint.Fixed(12)),
            h.Cell("Age").Width(SizeHint.Fixed(6)).Align(Alignment.Right),
            h.Cell("Status").Width(SizeHint.Fixed(10))
        ])
        .Row((r, row, state) => [
            r.Cell(row.Name),
            r.Cell(row.Role),
            r.Cell(row.Age.ToString()),
            r.Cell(row.Status)
        ])
        .Focus(focusedKey)
        .OnFocusChanged(key => onFocusChanged(key))
        .FillHeight();

    if (!isCompact)
        table = table.Full();

    if (isFillWidth)
        table = table.FillWidth();

    if (showSelection)
        table = table.SelectionColumn(
            e => e.IsSelected,
            (e, selected) => e.IsSelected = selected);

    var onOff = new[] { "On", "Off" };

    return ctx.VStack(v => [
        v.HStack(h => [
            h.Text(" Compact "),
            h.ToggleSwitch(onOff, isCompact ? 0 : 1)
                .OnSelectionChanged(e => onCompactChanged(e.SelectedIndex == 0)),
            h.Text("  Fill Width "),
            h.ToggleSwitch(onOff, isFillWidth ? 0 : 1)
                .OnSelectionChanged(e => onFillWidthChanged(e.SelectedIndex == 0)),
            h.Text("  Selection "),
            h.ToggleSwitch(onOff, showSelection ? 0 : 1)
                .OnSelectionChanged(e => onSelectionChanged(e.SelectedIndex == 0)),
        ]),
        table
    ]);
}

// Employee record for table data
class Employee(string name, string role, int age, string status)
{
    public string Name { get; } = name;
    public string Role { get; } = role;
    public int Age { get; } = age;
    public string Status { get; } = status;
    public bool IsSelected { get; set; }
}

