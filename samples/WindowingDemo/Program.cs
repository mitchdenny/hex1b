using Hex1b.Events;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Widgets;
using WindowingDemo;

const int InitialTerminalColumns = 80;
const int InitialTerminalRows = 24;
const int WindowChromeWidth = 2;
const int WindowChromeHeight = 3;
const int MinimumTerminalColumns = 40;
const int MinimumTerminalRows = 12;

if (!TryParseInnerTerminalMode(args, out var innerTerminalWindowsPtyMode, out var innerTerminalModeLabel, out var parseError, out var showHelp))
{
    if (!string.IsNullOrWhiteSpace(parseError))
    {
        if (showHelp)
        {
            await Console.Out.WriteLineAsync(parseError);
        }
        else
        {
            await Console.Error.WriteLineAsync(parseError);
        }
    }

    Environment.ExitCode = showHelp ? 0 : 1;
    return;
}

// Demo state
var windowCounter = 0;
var openWindowCount = 0;
var statusMessage = OperatingSystem.IsWindows()
    ? $"Ready (inner PTY mode: {innerTerminalModeLabel})"
    : "Ready";

// Track terminal instances for cleanup (using WindowHandle as key)
var terminalInstances = new Dictionary<WindowHandle, (Hex1bTerminal Terminal, CancellationTokenSource Cts)>();

// About window handle (for singleton pattern)
WindowHandle? aboutWindow = null;

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

void OpenTerminalWindow(MenuItemActivatedEventArgs e, TerminalShellKind shellKind)
{
    if (!TryGetTerminalLaunchSpec(shellKind, out var launchSpec, out var errorMessage))
    {
        statusMessage = errorMessage;
        return;
    }

    windowCounter++;
    openWindowCount++;
    var num = windowCounter;

    var cts = new CancellationTokenSource();
    var shellTerminal = Hex1bTerminal.CreateBuilder()
        // Window sizes include borders/title bar. Launch the PTY at the actual
        // terminal content size so the first prompt is laid out correctly.
        .WithDimensions(InitialTerminalColumns, InitialTerminalRows)
        .WithMouse()
        .WithPtyProcess(options =>
        {
            options.FileName = launchSpec.FileName;
            options.Arguments = launchSpec.Arguments;
            if (OperatingSystem.IsWindows())
            {
                options.WindowsPtyMode = innerTerminalWindowsPtyMode;
            }
        })
        .WithTerminalWidget(out var shellHandle)
        .Build();

    _ = RunTerminalWindowAsync(shellTerminal, cts, launchSpec.DisplayName, num);

    var window = e.Windows.Window(_ => new TerminalWidget(shellHandle))
        .Title($"{launchSpec.WindowTitle} {num}")
        .Size(InitialTerminalColumns + WindowChromeWidth, InitialTerminalRows + WindowChromeHeight)
        .Position(new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 2, OffsetY: num - 1))
        .Resizable(
            minWidth: MinimumTerminalColumns + WindowChromeWidth,
            minHeight: MinimumTerminalRows + WindowChromeHeight)
        .OnClose(() =>
        {
            openWindowCount--;
            statusMessage = $"Closed: {launchSpec.DisplayName} {num}";
            DisposeTerminalInstance(shellTerminal);
        });

    terminalInstances[window] = (shellTerminal, cts);

    e.Windows.Open(window);
    statusMessage = OperatingSystem.IsWindows()
        ? $"Opened: {launchSpec.DisplayName} {num} via {innerTerminalModeLabel}"
        : $"Opened: {launchSpec.DisplayName} {num}";
}

static bool TryParseInnerTerminalMode(
    string[] args,
    out WindowsPtyMode mode,
    out string modeLabel,
    out string? error,
    out bool showHelp)
{
    mode = WindowsPtyMode.RequireProxy;
    modeLabel = "proxy";
    error = null;
    showHelp = false;

    WindowsPtyMode? requestedMode = null;

    foreach (var arg in args)
    {
        if (string.Equals(arg, "--proxy-mode", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedMode == WindowsPtyMode.Direct)
            {
                error = "Specify only one of --proxy-mode or --direct-mode.";
                return false;
            }

            requestedMode = WindowsPtyMode.RequireProxy;
            continue;
        }

        if (string.Equals(arg, "--direct-mode", StringComparison.OrdinalIgnoreCase))
        {
            if (requestedMode == WindowsPtyMode.RequireProxy)
            {
                error = "Specify only one of --proxy-mode or --direct-mode.";
                return false;
            }

            requestedMode = WindowsPtyMode.Direct;
            continue;
        }

        if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
        {
            error = "Usage: dotnet run -- [--proxy-mode|--direct-mode]";
            showHelp = true;
            return false;
        }

        error = $"Unrecognized argument '{arg}'. Usage: dotnet run -- [--proxy-mode|--direct-mode]";
        return false;
    }

    mode = requestedMode ?? WindowsPtyMode.RequireProxy;
    modeLabel = mode == WindowsPtyMode.Direct ? "direct mode" : "proxy mode";
    return true;
}

async Task RunTerminalWindowAsync(Hex1bTerminal shellTerminal, CancellationTokenSource cts, string displayName, int num)
{
    try
    {
        await shellTerminal.RunAsync(cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
    }
    catch (ObjectDisposedException) when (cts.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
        statusMessage = $"{displayName} {num} failed: {ex.GetBaseException().Message}";
    }
}

void DisposeTerminalInstance(Hex1bTerminal shellTerminal)
{
    (Hex1bTerminal Terminal, CancellationTokenSource Cts)? terminalToDispose = null;
    WindowHandle? handleToRemove = null;

    foreach (var kvp in terminalInstances)
    {
        if (!ReferenceEquals(kvp.Value.Terminal, shellTerminal))
        {
            continue;
        }

        terminalToDispose = kvp.Value;
        handleToRemove = kvp.Key;
        break;
    }

    if (handleToRemove != null)
    {
        terminalInstances.Remove(handleToRemove);
    }

    if (terminalToDispose is { } instance)
    {
        _ = CleanupTerminalInstanceAsync(instance.Terminal, instance.Cts);
    }
}

async Task CleanupTerminalInstanceAsync(Hex1bTerminal terminalToDispose, CancellationTokenSource ctsToDispose)
{
    ctsToDispose.Cancel();
    try
    {
        await terminalToDispose.DisposeAsync();
    }
    finally
    {
        ctsToDispose.Dispose();
    }
}

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics("WindowingDemo", forceEnable: true)
    .WithMouse()
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
                        
                        var window = e.Windows.Window(w => w.VStack(v => [
                            v.Text(""),
                            v.Text($"  This is Window #{num}"),
                            v.Text(""),
                            v.Text("  Features:"),
                            v.Text("  • Drag title bar to move"),
                            v.Text("  • Click buttons to interact"),
                            v.Text("  • Press Escape to close"),
                            v.Text(""),
                            v.HStack(h => [
                                h.Text("  "),
                                h.Button("Action").OnClick(_ => statusMessage = $"Action from Window {num}!"),
                                h.Text(" "),
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]))
                        .Title($"Window {num}")
                        .Size(45, 12)
                        .Position(new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 3, OffsetY: (num - 1) * 2))
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Window {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Window {num}";
                    }),
                    m.MenuItem("New Window with Custom Actions").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        var actionFeedback = "";
                        
                        var window = e.Windows.Window(w => w.VStack(v => [
                            v.Text(""),
                            v.Text($"  Custom Actions Window #{num}"),
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
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]))
                        .Title($"Custom Actions {num}")
                        .Size(50, 12)
                        .Position(new WindowPositionSpec(WindowPosition.Center, OffsetX: (num - 1) * 3, OffsetY: (num - 1) * 2))
                        .LeftTitleActions(t => [
                            t.Action("📌", _ => { 
                                actionFeedback = "📌 Window pinned!";
                                statusMessage = $"Pinned Window {num}"; 
                            }),
                            t.Action("📋", _ => { 
                                actionFeedback = "📋 Content copied!";
                                statusMessage = $"Copied from Window {num}"; 
                            })
                        ])
                        .RightTitleActions(t => [
                            t.Action("?", _ => { 
                                actionFeedback = "❓ Help requested!";
                                statusMessage = $"Help for Window {num}"; 
                            }),
                            t.Close()
                        ])
                        .OnClose(() => { 
                            openWindowCount--; 
                            statusMessage = $"Closed: Custom Actions {num}"; 
                        });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Custom Actions {num}";
                    }),
                    m.MenuItem("New PowerShell Terminal").OnActivated(e => OpenTerminalWindow(e, TerminalShellKind.PowerShell)),
                    m.MenuItem("New Cmd Terminal").OnActivated(e => OpenTerminalWindow(e, TerminalShellKind.CommandPrompt)),
                    m.MenuItem("New Bash Terminal").OnActivated(e => OpenTerminalWindow(e, TerminalShellKind.Bash)),
                    m.MenuItem("New Full Chrome Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(w => w.VStack(v => [
                            v.Text(""),
                            v.Text($"  This is Full Chrome Window #{num}"),
                            v.Text(""),
                            v.Text("  Features:"),
                            v.Text("  • Drag title bar to move"),
                            v.Text("  • Click buttons to interact"),
                            v.Text("  • Press Escape to close"),
                            v.Text(""),
                            v.HStack(h => [
                                h.Text("  "),
                                h.Button("Action").OnClick(_ => statusMessage = $"Action from Full Chrome {num}!"),
                                h.Text(" "),
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]))
                        .Title($"Full Chrome {num}")
                        .Size(45, 12)
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Full Chrome {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Full Chrome {num}";
                    }),
                    m.MenuItem("New Frameless Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(w => w.VStack(v => [
                            v.Text($"  Frameless Window #{num}"),
                            v.Text(""),
                            v.Text("  No title bar - just content."),
                            v.Text("  Press Escape to close."),
                            v.Text(""),
                            v.HStack(h => [
                                h.Text("  "),
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]))
                        .Size(35, 8)
                        .NoTitleBar()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Frameless {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Frameless {num}";
                    }),
                    m.MenuItem("New Resizable Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(w => w.VStack(v => [
                            v.Text(""),
                            v.Text($"  Resizable Window #{num}"),
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
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]).Fill())
                        .Title($"Resizable {num}")
                        .Size(50, 15)
                        .Resizable(minWidth: 30, minHeight: 10, maxWidth: 80, maxHeight: 30)
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Resizable {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Resizable {num}";
                    }),
                    m.MenuItem("New Table Window").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(w => BuildTableContent(w, tableData, tableCompactMode, tableFillWidth, tableShowSelection,
                            tableFocusedKey, key => tableFocusedKey = key,
                            isCompact => tableCompactMode = isCompact,
                            isFill => tableFillWidth = isFill,
                            showSel => tableShowSelection = showSel))
                        .Title($"Employee Table {num}")
                        .Size(65, 18)
                        .Resizable(minWidth: 50, minHeight: 10)
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Table {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Table {num}";
                    }),
                    m.Separator(),
                    m.MenuItem("Close All Windows").OnActivated(e => {
                        e.Windows.CloseAll();
                        openWindowCount = 0;
                        aboutWindow = null;
                        // Clean up all terminal instances
                        var instances = terminalInstances.Values.ToArray();
                        terminalInstances.Clear();
                        foreach (var instance in instances)
                        {
                            _ = CleanupTerminalInstanceAsync(instance.Terminal, instance.Cts);
                        }
                        statusMessage = "Closed all windows";
                    }),
                    m.Separator(),
                    m.MenuItem("Exit").OnActivated(e => e.Context.RequestStop())
                ]),
                m.Menu("Window", m => [
                    m.MenuItem("Open Modal Dialog").OnActivated(e => {
                        openWindowCount++;
                        
                        var modal = e.Windows.Window(w => w.VStack(v => [
                            v.Text(""),
                            v.Text("  ⚠️  This is a modal dialog!"),
                            v.Text(""),
                            v.Text("  Background windows are blocked."),
                            v.Text(""),
                            v.HStack(h => [
                                h.Text("  "),
                                h.Button("OK").OnClick(ev => {
                                    ev.Windows.Get(w.Window)?.CloseWithResult(true);
                                }),
                                h.Text(" "),
                                h.Button("Cancel").OnClick(ev => {
                                    ev.Windows.Get(w.Window)?.CloseWithResult(false);
                                })
                            ])
                        ]))
                        .Title("Modal Dialog")
                        .Size(50, 10)
                        .Modal()
                        .OnClose(() => { openWindowCount--; statusMessage = "Modal closed"; });
                        
                        e.Windows.Open(modal);
                        statusMessage = "Opened modal dialog";
                    }),
                    m.MenuItem("Confirm Delete").OnActivated(e => {
                        openWindowCount++;
                        
                        var confirm = e.Windows.Window(w => w.VStack(v => [
                            v.Text(""),
                            v.Text("  🗑️  Delete all items?"),
                            v.Text(""),
                            v.Text("  This action cannot be undone."),
                            v.Text(""),
                            v.HStack(h => [
                                h.Text("  "),
                                h.Button("Delete").OnClick(ev => {
                                    statusMessage = "Delete confirmed!";
                                    ev.Windows.Close(w.Window);
                                }),
                                h.Text(" "),
                                h.Button("Cancel").OnClick(ev => {
                                    statusMessage = "Delete cancelled";
                                    ev.Windows.Close(w.Window);
                                })
                            ])
                        ]))
                        .Title("Confirm Delete")
                        .Size(45, 10)
                        .Modal()
                        .OnClose(() => openWindowCount--);
                        
                        e.Windows.Open(confirm);
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
                                    
                                    var tableWindow = ctx.InputTrigger.Windows.Window(w => BuildTableContent(w, tableData, tableCompactMode, tableFillWidth, tableShowSelection,
                                        tableFocusedKey, key => tableFocusedKey = key,
                                        isCompact => tableCompactMode = isCompact,
                                        isFill => tableFillWidth = isFill,
                                        showSel => tableShowSelection = showSel))
                                    .Title($"Employee Table {num}")
                                    .Size(65, 18)
                                    .Resizable(minWidth: 50, minHeight: 10)
                                    .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Table {num}"; });
                                    
                                    ctx.InputTrigger.Windows.Open(tableWindow);
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
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => FirefliesDemo.BuildLayers(s, fireflies))
                            .Width(SizeHint.Fixed(FirefliesDemo.WidthCells))
                            .Height(SizeHint.Fixed(FirefliesDemo.HeightCells))
                            .RedrawAfter(50))
                        .Title($"Fireflies {num}")
                        .Size(FirefliesDemo.RequiredWidth, FirefliesDemo.RequiredHeight)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Fireflies {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Fireflies {num}";
                    }),
                    m.MenuItem("Radar").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => RadarDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Radar {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Radar {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Radar {num}";
                    }),
                    m.MenuItem("Gravity").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => GravityDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Gravity {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Gravity {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Gravity {num}";
                    }),
                    m.MenuItem("Snow").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => SnowDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Snow {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Snow {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Snow {num}";
                    }),
                    m.MenuItem("Noise").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => NoiseDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Noise {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Noise {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Noise {num}";
                    }),
                    m.MenuItem("Shadows").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => ShadowDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Shadows {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Shadows {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Shadows {num}";
                    }),
                    m.MenuItem("Fluid").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => FluidDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Fluid {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Fluid {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Fluid {num}";
                    }),
                    m.MenuItem("Smart Matter").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => SmartMatterDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Smart Matter {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Smart Matter {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Smart Matter {num}";
                    }),
                    m.MenuItem("Slime Mold").OnActivated(e => {
                        windowCounter++;
                        openWindowCount++;
                        var num = windowCounter;
                        
                        var window = e.Windows.Window(_ => new SurfaceWidget(s => SlimeMoldDemo.BuildLayers(s, demoRandom))
                            .Width(SizeHint.Fixed(60))
                            .Height(SizeHint.Fixed(20))
                            .RedrawAfter(50))
                        .Title($"Slime Mold {num}")
                        .Size(62, 22)
                        .Resizable()
                        .OnClose(() => { openWindowCount--; statusMessage = $"Closed: Slime Mold {num}"; });
                        
                        e.Windows.Open(window);
                        statusMessage = $"Opened: Slime Mold {num}";
                    })
                ]),
                m.Menu("Help", m => [
                    m.MenuItem("About").OnActivated(e => {
                        // Singleton pattern using stored handle
                        if (aboutWindow != null && e.Windows.IsOpen(aboutWindow)) {
                            e.Windows.BringToFront(aboutWindow);
                            return;
                        }
                        
                        openWindowCount++;
                        aboutWindow = e.Windows.Window(w => w.VStack(v => [
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
                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                            ])
                        ]))
                        .Title("About")
                        .Size(40, 13)
                        .OnClose(() => { openWindowCount--; aboutWindow = null; statusMessage = "About closed"; });
                        
                        e.Windows.Open(aboutWindow);
                        statusMessage = "Opened About";
                    })
                ])
            ]),

            // ─────────────────────────────────────────────────────────────────
            // WINDOW PANEL (MDI area) - Unbounded allows windows to go outside
            // ─────────────────────────────────────────────────────────────────
            main.WindowPanel()
            .Background(b =>
                b.Surface(s => SlimeMoldBackground.BuildLayers(s, demoRandom))
                    .RedrawAfter(SlimeMoldBackground.RecommendedRedrawMs)
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

static bool TryGetTerminalLaunchSpec(
    TerminalShellKind shellKind,
    out TerminalLaunchSpec launchSpec,
    out string errorMessage)
{
    if (OperatingSystem.IsWindows())
    {
        switch (shellKind)
        {
            case TerminalShellKind.PowerShell:
                var pwshPath = TryFindExecutable("pwsh.exe");
                if (pwshPath is null)
                {
                    launchSpec = default!;
                    errorMessage = "Cannot open PowerShell terminal: pwsh.exe was not found on PATH.";
                    return false;
                }

                launchSpec = new TerminalLaunchSpec(
                    "PowerShell",
                    "PowerShell Terminal",
                    pwshPath,
                    ["-NoLogo", "-NoProfile"]);
                errorMessage = string.Empty;
                return true;

            case TerminalShellKind.CommandPrompt:
                var cmdPath = TryFindExecutable("cmd.exe");
                if (cmdPath is null)
                {
                    launchSpec = default!;
                    errorMessage = "Cannot open Command Prompt terminal: cmd.exe was not found.";
                    return false;
                }

                launchSpec = new TerminalLaunchSpec(
                    "cmd.exe",
                    "Command Prompt",
                    cmdPath,
                    []);
                errorMessage = string.Empty;
                return true;

            case TerminalShellKind.Bash:
                var bashPath = TryFindExecutable("bash.exe");
                if (bashPath is null)
                {
                    launchSpec = default!;
                    errorMessage = "Cannot open WSL Bash terminal: bash.exe was not found.";
                    return false;
                }

                launchSpec = new TerminalLaunchSpec(
                    "WSL bash.exe",
                    "WSL Bash Terminal",
                    bashPath,
                    []);
                errorMessage = string.Empty;
                return true;
        }
    }

    switch (shellKind)
    {
        case TerminalShellKind.PowerShell:
            var pwsh = TryFindExecutable("pwsh");
            if (pwsh is null)
            {
                launchSpec = default!;
                errorMessage = "Cannot open PowerShell terminal: pwsh was not found on PATH.";
                return false;
            }

            launchSpec = new TerminalLaunchSpec(
                "PowerShell",
                "PowerShell Terminal",
                pwsh,
                ["-NoLogo", "-NoProfile"]);
            errorMessage = string.Empty;
            return true;

        case TerminalShellKind.CommandPrompt:
            launchSpec = default!;
            errorMessage = "Command Prompt terminals are only available on Windows.";
            return false;

        case TerminalShellKind.Bash:
            var bash = TryFindExecutable("bash");
            if (bash is null)
            {
                launchSpec = default!;
                errorMessage = "Cannot open Bash terminal: bash was not found on PATH.";
                return false;
            }

            launchSpec = new TerminalLaunchSpec(
                "bash",
                "Bash Terminal",
                bash,
                []);
            errorMessage = string.Empty;
            return true;

        default:
            launchSpec = default!;
            errorMessage = $"Unsupported terminal shell: {shellKind}.";
            return false;
    }
}

static string? TryFindExecutable(string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return null;
    }

    if (Path.IsPathRooted(fileName) && File.Exists(fileName))
    {
        return fileName;
    }

    if (OperatingSystem.IsWindows())
    {
        try
        {
            var systemCandidate = Path.Combine(Environment.SystemDirectory, fileName);
            if (File.Exists(systemCandidate))
            {
                return systemCandidate;
            }
        }
        catch (ArgumentException)
        {
        }
    }

    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        try
        {
            var candidate = Path.Combine(entry, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        catch (ArgumentException)
        {
        }
    }

    return null;
}

// Helper to build table content - now uses WindowContentContext
static Hex1bWidget BuildTableContent(
    WindowContentContext<Hex1bWidget> ctx,
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

enum TerminalShellKind
{
    PowerShell,
    CommandPrompt,
    Bash
}

sealed record TerminalLaunchSpec(
    string DisplayName,
    string WindowTitle,
    string FileName,
    string[] Arguments);

