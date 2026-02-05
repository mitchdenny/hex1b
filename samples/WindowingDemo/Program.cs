using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using WindowingDemo;

// Demo state
var windowCounter = 0;
var openWindowCount = 0;
var statusMessage = "Ready";

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

await using var terminal = Hex1bTerminal.CreateBuilder()
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
                            id: $"window-{num}",
                            title: $"Window {num}",
                            content: () => BuildWindowContent(num),
                            width: 45,
                            height: 12,
                            position: new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 3, OffsetY: (num - 1) * 2),
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Window {num}"; }
                        );
                        statusMessage = $"Opened: Window {num}";
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
                            id: windowId,
                            title: $"Terminal {num}",
                            content: () => new TerminalWidget(bashHandle),
                            width: 80,
                            height: 24,
                            position: new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 2, OffsetY: (num - 1)),
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            minWidth: 40,
                            minHeight: 12,
                            onClose: () => {
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
                        );
                        statusMessage = $"Opened: Terminal {num}";
                    }),
                    m.MenuItem("New Full Chrome Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"full-{num}",
                            title: $"Full Chrome {num}",
                            content: () => BuildWindowContent(num),
                            width: 45,
                            height: 12,
                            chromeStyle: WindowChromeStyle.Full,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Window {num}"; }
                        );
                        statusMessage = $"Opened: Full Chrome {num}";
                    }),
                    m.MenuItem("New Frameless Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"frame-{num}",
                            title: $"Frameless {num}",
                            content: () => BuildFramelessContent(num),
                            width: 35,
                            height: 8,
                            chromeStyle: WindowChromeStyle.None,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Frameless {num}"; }
                        );
                        statusMessage = $"Opened: Frameless {num}";
                    }),
                    m.MenuItem("New Resizable Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"resizable-{num}",
                            title: $"Resizable {num}",
                            content: () => BuildResizableContent(num),
                            width: 50,
                            height: 15,
                            chromeStyle: WindowChromeStyle.Full,
                            isResizable: true,
                            minWidth: 30,
                            minHeight: 10,
                            maxWidth: 80,
                            maxHeight: 30,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Resizable {num}"; }
                        );
                        statusMessage = $"Opened: Resizable {num}";
                    }),
                    m.MenuItem("New Table Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"table-{num}",
                            title: $"Employee Table {num}",
                            content: () => BuildTableContent(tableData),
                            width: 65,
                            height: 18,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            minWidth: 50,
                            minHeight: 10,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Table {num}"; }
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
                            id: "modal",
                            title: "Modal Dialog",
                            content: () => new VStackWidget([
                                new TextBlockWidget(""),
                                new TextBlockWidget("  ⚠️  This is a modal dialog!"),
                                new TextBlockWidget(""),
                                new TextBlockWidget("  Background windows are blocked."),
                                new TextBlockWidget(""),
                                new HStackWidget([
                                    new TextBlockWidget("  "),
                                    new ButtonWidget("OK").OnClick(ev => {
                                        ev.Context.Windows.Get("modal")?.CloseWithResult(true);
                                    }),
                                    new TextBlockWidget(" "),
                                    new ButtonWidget("Cancel").OnClick(ev => {
                                        ev.Context.Windows.Get("modal")?.CloseWithResult(false);
                                    })
                                ])
                            ]),
                            width: 50,
                            height: 10,
                            isModal: true,
                            onClose: () => { openWindowCount--; statusMessage = "Modal closed"; }
                        );
                        statusMessage = "Opened modal dialog";
                    }),
                    m.MenuItem("Confirm Delete").OnActivated(e => {
                        openWindowCount++;
                        e.Windows.Open(
                            id: "confirm-delete",
                            title: "Confirm Delete",
                            content: () => new VStackWidget([
                                new TextBlockWidget(""),
                                new TextBlockWidget("  🗑️  Delete all items?"),
                                new TextBlockWidget(""),
                                new TextBlockWidget("  This action cannot be undone."),
                                new TextBlockWidget(""),
                                new HStackWidget([
                                    new TextBlockWidget("  "),
                                    new ButtonWidget("Delete").OnClick(ev => {
                                        statusMessage = "Delete confirmed!";
                                        ev.Context.Windows.Get("confirm-delete")?.Close();
                                    }),
                                    new TextBlockWidget(" "),
                                    new ButtonWidget("Cancel").OnClick(ev => {
                                        statusMessage = "Delete cancelled";
                                        ev.Context.Windows.Get("confirm-delete")?.Close();
                                    })
                                ])
                            ]),
                            width: 45,
                            height: 10,
                            isModal: true,
                            onClose: () => openWindowCount--
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
                                        id: $"table-notif-{num}",
                                        title: $"Employee Table {num}",
                                        content: () => BuildTableContent(tableData),
                                        width: 65,
                                        height: 18,
                                        chromeStyle: WindowChromeStyle.TitleAndClose,
                                        isResizable: true,
                                        minWidth: 50,
                                        minHeight: 10,
                                        onClose: () => { openWindowCount--; statusMessage = $"Closed: Table {num}"; }
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
                            id: $"fireflies-{num}",
                            title: $"Fireflies {num}",
                            content: () => new SurfaceWidget(s => FirefliesDemo.BuildLayers(s, fireflies))
                                .Width(SizeHint.Fixed(FirefliesDemo.WidthCells))
                                .Height(SizeHint.Fixed(FirefliesDemo.HeightCells))
                                .RedrawAfter(50),
                            width: FirefliesDemo.RequiredWidth,
                            height: FirefliesDemo.RequiredHeight,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Fireflies {num}"; }
                        );
                        statusMessage = $"Opened: Fireflies {num}";
                    }),
                    m.MenuItem("Radar").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"radar-{num}",
                            title: $"Radar {num}",
                            content: () => new SurfaceWidget(s => RadarDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Radar {num}"; }
                        );
                        statusMessage = $"Opened: Radar {num}";
                    }),
                    m.MenuItem("Gravity").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"gravity-{num}",
                            title: $"Gravity {num}",
                            content: () => new SurfaceWidget(s => GravityDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Gravity {num}"; }
                        );
                        statusMessage = $"Opened: Gravity {num}";
                    }),
                    m.MenuItem("Snow").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"snow-{num}",
                            title: $"Snow {num}",
                            content: () => new SurfaceWidget(s => SnowDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Snow {num}"; }
                        );
                        statusMessage = $"Opened: Snow {num}";
                    }),
                    m.MenuItem("Noise").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"noise-{num}",
                            title: $"Noise {num}",
                            content: () => new SurfaceWidget(s => NoiseDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Noise {num}"; }
                        );
                        statusMessage = $"Opened: Noise {num}";
                    }),
                    m.MenuItem("Shadows").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"shadows-{num}",
                            title: $"Shadows {num}",
                            content: () => new SurfaceWidget(s => ShadowDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Shadows {num}"; }
                        );
                        statusMessage = $"Opened: Shadows {num}";
                    }),
                    m.MenuItem("Fluid").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"fluid-{num}",
                            title: $"Fluid {num}",
                            content: () => new SurfaceWidget(s => FluidDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Fluid {num}"; }
                        );
                        statusMessage = $"Opened: Fluid {num}";
                    }),
                    m.MenuItem("Smart Matter").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"smartmatter-{num}",
                            title: $"Smart Matter {num}",
                            content: () => new SurfaceWidget(s => SmartMatterDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Smart Matter {num}"; }
                        );
                        statusMessage = $"Opened: Smart Matter {num}";
                    }),
                    m.MenuItem("Slime Mold").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        e.Windows.Open(
                            id: $"slimemold-{num}",
                            title: $"Slime Mold {num}",
                            content: () => new SurfaceWidget(s => SlimeMoldDemo.BuildLayers(s, demoRandom))
                                .Width(SizeHint.Fixed(60))
                                .Height(SizeHint.Fixed(20))
                                .RedrawAfter(50),
                            width: 62,
                            height: 22,
                            chromeStyle: WindowChromeStyle.TitleAndClose,
                            isResizable: true,
                            onClose: () => { openWindowCount--; statusMessage = $"Closed: Slime Mold {num}"; }
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
                            id: "about",
                            title: "About",
                            content: () => new VStackWidget([
                                new TextBlockWidget(""),
                                new TextBlockWidget("  Hex1b Floating Windows Demo"),
                                new TextBlockWidget("  Version: 1.0.0"),
                                new TextBlockWidget(""),
                                new TextBlockWidget("  Features:"),
                                new TextBlockWidget("  • Multiple window styles"),
                                new TextBlockWidget("  • Drag to move"),
                                new TextBlockWidget("  • Min/Max/Close buttons"),
                                new TextBlockWidget(""),
                                new HStackWidget([
                                    new TextBlockWidget("  "),
                                    new ButtonWidget("Close")
                                ])
                            ]),
                            width: 40,
                            height: 13,
                            onClose: () => { openWindowCount--; statusMessage = "About closed"; }
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

// Build window content
static Hex1bWidget BuildWindowContent(int windowNum)
{
    return new VStackWidget([
        new TextBlockWidget(""),
        new TextBlockWidget($"  This is Window #{windowNum}"),
        new TextBlockWidget(""),
        new TextBlockWidget("  Features:"),
        new TextBlockWidget("  • Drag title bar to move"),
        new TextBlockWidget("  • Click buttons to interact"),
        new TextBlockWidget("  • Press Escape to close"),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("Action"),
            new TextBlockWidget(" "),
            new ButtonWidget("Close")
        ])
    ]);
}

static Hex1bWidget BuildFramelessContent(int windowNum)
{
    return new VStackWidget([
        new TextBlockWidget($"  Frameless Window #{windowNum}"),
        new TextBlockWidget(""),
        new TextBlockWidget("  No title bar - just content."),
        new TextBlockWidget("  Press Escape to close."),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("Close")
        ])
    ]);
}

static Hex1bWidget BuildResizableContent(int windowNum)
{
    return new VStackWidget([
        new TextBlockWidget(""),
        new TextBlockWidget($"  Resizable Window #{windowNum}"),
        new TextBlockWidget(""),
        new TextBlockWidget("  🖱️  Resize Handles:"),
        new TextBlockWidget("  • Drag left/right edges"),
        new TextBlockWidget("  • Drag bottom edge"),
        new TextBlockWidget("  • Drag corners (◢)"),
        new TextBlockWidget(""),
        new TextBlockWidget("  📏 Constraints:"),
        new TextBlockWidget("  • Min: 30×10"),
        new TextBlockWidget("  • Max: 80×30"),
        new TextBlockWidget(""),
        new HStackWidget([
            new TextBlockWidget("  "),
            new ButtonWidget("Close")
        ])
    ]).Fill();
}

static Hex1bWidget BuildTableContent(IReadOnlyList<Employee> data)
{
    return new VStackWidget([
        new TableWidget<Employee> { Data = data }
            .Header(h => [
                h.Cell("Name").Width(SizeHint.Fixed(18)),
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
            .Fill()
    ]);
}

// Employee record for table data
record Employee(string Name, string Role, int Age, string Status);

