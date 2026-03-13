using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for window focus behavior.
/// </summary>
public class WindowFocusIntegrationTests
{
    /// <summary>
    /// Tests the real scenario: MenuBar + WindowPanel + StatusBar layout.
    /// Opens a window via menu, then verifies ALT-F opens the menu again.
    /// </summary>
    [Fact]
    public async Task MenuBar_KeyboardShortcuts_WorkAfterWindowOpened()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var windowOpened = false;
        var menuItemActivatedCount = 0;
        var statusText = "Ready";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    // MenuBar at top
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New Window").OnActivated(e =>
                            {
                                menuItemActivatedCount++;
                                statusText = $"Menu activated {menuItemActivatedCount} times";
                                var handle = e.Windows.Window(w => w.Button("Window Content").OnClick(_ => {}))
                                    .Title("Test Window")
                                    .Size(30, 10)
                                    .Position(new WindowPositionSpec(WindowPosition.Center))
                                    .OnClose(() => { windowOpened = false; statusText = "Window closed"; });
                                e.Windows.Open(handle);
                                windowOpened = true;
                            }),
                            menu.MenuItem("Exit").OnActivated(e =>
                            {
                                menuItemActivatedCount++;
                                statusText = "Exit clicked";
                            })
                        ]),
                        m.Menu("Edit", menu => [
                            menu.MenuItem("Copy").OnActivated(e =>
                            {
                                menuItemActivatedCount++;
                                statusText = "Copy clicked";
                            })
                        ])
                    ]),
                    // WindowPanel in middle (fills remaining space)
                    outer.WindowPanel()
                        .Height(SizeHint.Fill),
                    // StatusBar at bottom
                    outer.HStack(status => [
                        status.Text("Status: ").Width(SizeHint.Content),
                        status.Text(statusText).Width(SizeHint.Fill)
                    ])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Step 1: Use ALT-F to open File menu via keyboard shortcut
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Alt().Key(Hex1bKey.F)  // ALT-F to open File menu
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify: Menu opened (New Window is visible)
        Assert.Equal(0, menuItemActivatedCount); // Menu just opened, nothing activated yet
        
        // Step 2: Press Enter to activate New Window (first item is focused)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)  // Activate "New Window"
            .WaitUntil(s => s.ContainsText("Test Window"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify: Window opened and menu item was activated
        Assert.True(windowOpened, "Window should have been opened");
        Assert.Equal(1, menuItemActivatedCount); // One menu item activated
        
        // Step 3: NOW THE KEY TEST - Can we still use ALT-F to open menu while window is open?
        string? step3State = null;
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Alt().Key(Hex1bKey.F)  // ALT-F to open File menu again
                .WaitUntil(s => 
                {
                    step3State = s.ToString();
                    return s.ContainsText("New Window") && s.ContainsText("Exit");
                }, TimeSpan.FromSeconds(1))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        }
        catch (TimeoutException ex)
        {
            Assert.Fail($"ALT-F failed to open menu after window was opened. State: {step3State}. Exception: {ex.Message}");
        }

        // Step 4: Navigate to Exit and activate it
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)  // Move to Exit
            .WaitUntil(s => s.ContainsText("Exit"), TimeSpan.FromSeconds(5), "Waiting for Exit to be visible")
            .Key(Hex1bKey.Enter)  // Activate Exit
            .WaitUntil(_ => menuItemActivatedCount == 2, TimeSpan.FromSeconds(5), "Waiting for Exit activation")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Step 5: Verify Exit was activated (proves keyboard navigation worked)
        Assert.Equal(2, menuItemActivatedCount);
        Assert.Equal("Exit clicked", statusText);

        // Cleanup
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    /// <summary>
    /// Tests that arrow key navigation works in menu popups when a window is open.
    /// Root cause: MenuBarNode.SetFocus() sets IsFocused directly on MenuNode without
    /// clearing it when popup opens, creating dual-focus (MenuNode + MenuItemNode).
    /// InputRouter.BuildPathToFocused finds MenuNode first (depth-first), so DownArrow
    /// routes to MenuNode's binding (OpenMenu no-op) instead of MenuPopupNode's binding (FocusNext).
    /// </summary>
    [Fact(Skip = "MenuBarNode.SetFocus creates stale IsFocused on MenuNode that confuses InputRouter when popup is open")]
    public async Task MenuBar_ArrowNavigation_WorksWithWindowOpen()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var activatedItems = new List<string>();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New Window").OnActivated(e =>
                            {
                                var handle = e.Windows.Window(w => w.Button("Content").OnClick(_ => {}))
                                    .Title("Window")
                                    .Size(20, 5);
                                e.Windows.Open(handle);
                            }),
                            menu.MenuItem("Item 1").OnActivated(e => { activatedItems.Add("Item 1"); }),
                            menu.MenuItem("Item 2").OnActivated(e => { activatedItems.Add("Item 2"); }),
                            menu.MenuItem("Item 3").OnActivated(e => { activatedItems.Add("Item 3"); })
                        ])
                    ]),
                    outer.WindowPanel()
                        .Height(SizeHint.Fill),
                    outer.Text("Status bar")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // First, open a window via File menu
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Alt().Key(Hex1bKey.F)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Now open menu again while window is open
        await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.F)
            .WaitUntil(s => s.ContainsText("Item 1") && s.ContainsText("Item 3"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Navigate down to Item 1 and activate
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => activatedItems.Count > 0, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("Item 1", activatedItems[0]);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }



    [Fact]
    public async Task OpeningSecondWindow_FocusesSecondWindow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var window1Closed = false;
        var window2Closed = false;

        // Open both windows in a single button click to ensure both exist
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.Button("Open Both Windows").OnClick(e =>
                    {
                        // Offset windows so both are visible
                        var handle1 = e.Windows.Window(_ => new ButtonWidget("W1 Button").OnClick(_ => {}))
                            .Title("Window 1")
                            .Size(30, 8)
                            .Position(new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 2, OffsetY: 2))
                            .OnClose(() => window1Closed = true);
                        e.Windows.Open(handle1);
                        var handle2 = e.Windows.Window(_ => new ButtonWidget("W2 Button").OnClick(_ => {}))
                            .Title("Window 2")
                            .Size(30, 8)
                            .Position(new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 35, OffsetY: 2))
                            .OnClose(() => window2Closed = true);
                        e.Windows.Open(handle2);
                    }),
                    outer.WindowPanel().Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open both windows at once, then press ESC
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open Both Windows"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)  // Click button to open both windows
            .WaitUntil(s => s.ContainsText("Window 1") && s.ContainsText("Window 2"), TimeSpan.FromSeconds(5))
            // Window 2 is active (opened last), so ESC should close it
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => window1Closed || window2Closed, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Window 2 should be closed (it was active), Window 1 should still be open
        Assert.True(window2Closed, $"Window 2 should have been closed by ESC (it was the active window). W1Closed={window1Closed}, W2Closed={window2Closed}. FocusPath={InputRouter.LastPathDebug}");
        Assert.False(window1Closed, "Window 1 should NOT have been closed (it was not the active window)");
    }

    [Fact]
    public async Task ClickingWindow_MakesItActive_EscapeClosesCorrectWindow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var window1Closed = false;
        var window2Closed = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.Button("Open Both").OnClick(e =>
                    {
                        var handle1 = e.Windows.Window(_ => new TextBlockWidget("Content 1"))
                            .Title("Window 1")
                            .Size(30, 10)
                            .Position(new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 2, OffsetY: 2))
                            .OnClose(() => window1Closed = true);
                        e.Windows.Open(handle1);
                        var handle2 = e.Windows.Window(_ => new TextBlockWidget("Content 2"))
                            .Title("Window 2")
                            .Size(30, 10)
                            .Position(new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 10, OffsetY: 5))
                            .OnClose(() => window2Closed = true);
                        e.Windows.Open(handle2);
                    }),
                    outer.WindowPanel().Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open both windows
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open Both"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window 2"), TimeSpan.FromSeconds(5))
            .Capture("both_windows")
            // Window 2 is on top and should be active
            // Click on Window 1 (at position 5, 3 which is inside window 1 but not inside window 2)
            .ClickAt(5, 3)
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(100)) // Brief wait for click to process
            .Capture("after_click_window1")
            // Now press Escape - should close Window 1 (the newly focused one)
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => window1Closed || window2Closed, TimeSpan.FromSeconds(5))
            .Capture("after_escape")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Window 1 should be closed (we clicked on it), Window 2 should still be open
        Assert.True(window1Closed, "Window 1 should have been closed by ESC (we clicked on it to focus it)");
        Assert.False(window2Closed, "Window 2 should NOT have been closed");
    }

    [Fact]
    public async Task OpeningWindowsViaMenu_SecondWindowGetsEscaped()
    {
        // This test mimics the actual user scenario - opening windows via menu bar
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var window1Closed = false;
        var window2Closed = false;
        var windowCounter = 0;
        string? focusedBeforeEsc = null;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New Window").OnActivated(e =>
                            {
                                windowCounter++;
                                var num = windowCounter;
                                var handle = e.Windows.Window(_ => new ButtonWidget($"Content {num}").OnClick(btn => 
                                    {
                                        focusedBeforeEsc = $"Window {num} button clicked";
                                    }))
                                    .Title($"Window {num}")
                                    .Size(30, 10)
                                    .OnClose(() => 
                                    { 
                                        if (num == 1) window1Closed = true; 
                                        else window2Closed = true; 
                                    });
                                e.Windows.Open(handle);
                            })
                        ])
                    ]),
                    outer.Text("Main content area"),
                    outer.WindowPanel().Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open two windows via menu
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            // Open File menu and select New Window (opens window 1)
            .Key(Hex1bKey.Enter)  // Opens File menu
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)  // Activates New Window
            .WaitUntil(s => s.ContainsText("Window 1"), TimeSpan.FromSeconds(5))
            // Click on the menu bar to open menu again
            .ClickAt(2, 0)  // Click File menu
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)  // Activates New Window (opens window 2)
            .WaitUntil(s => s.ContainsText("Window 2"), TimeSpan.FromSeconds(5))
            // Now press Escape - should close window 2 (the active one)
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => window1Closed || window2Closed, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var focusPath = InputRouter.LastPathDebug;
        
        // Window 2 should be closed (it was active), Window 1 should still be open
        Assert.True(window2Closed, $"Window 2 should have been closed by ESC. W1Closed={window1Closed}, W2Closed={window2Closed}. FocusPath={focusPath}");
        Assert.False(window1Closed, $"Window 1 should NOT have been closed. FocusPath={focusPath}");
    }

    /// <summary>
    /// Tests that a frameless window (NoTitleBar) opened from a menu can be closed with Escape.
    /// This is the exact scenario reported as broken in the WindowingDemo.
    /// </summary>
    [Fact]
    public async Task FramelessWindow_OpenedViaMenu_ClosesWithEscape()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var windowClosed = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New Frameless").OnActivated(e =>
                            {
                                var handle = e.Windows.Window(w => w.VStack(v => [
                                        v.Text("Frameless content"),
                                        v.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                                    ]))
                                    .Size(30, 8)
                                    .NoTitleBar()
                                    .OnClose(() => windowClosed = true);
                                e.Windows.Open(handle);
                            })
                        ])
                    ]),
                    outer.WindowPanel().Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("New Frameless"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Frameless content"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => windowClosed, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(windowClosed, $"Frameless window should have been closed by ESC. FocusPath={InputRouter.LastPathDebug}");
    }

    /// <summary>
    /// Tests that Escape closes a window when opened from a menu inside a NotificationPanel.
    /// This matches the exact widget tree structure of the WindowingDemo sample app:
    /// VStack → NotificationPanel(VStack(MenuBar, ..., WindowPanel))
    /// </summary>
    [Fact]
    public async Task Window_OpenedViaMenu_WithNotificationPanel_ClosesWithEscape()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var windowClosed = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.NotificationPanel(outer.VStack(main => [
                        main.MenuBar(m => [
                            m.Menu("File", menu => [
                                menu.MenuItem("New Window").OnActivated(e =>
                                {
                                    var handle = e.Windows.Window(w => w.VStack(v => [
                                            v.Text("Window content"),
                                            v.Text("Press Escape to close"),
                                            v.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                                        ]))
                                        .Title("Test Window")
                                        .Size(40, 10)
                                        .OnClose(() => windowClosed = true);
                                    e.Windows.Open(handle);
                                })
                            ])
                        ]),
                        main.WindowPanel().Height(SizeHint.Fill)
                    ])).Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window content"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => windowClosed, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(windowClosed, $"Window should have been closed by ESC with NotificationPanel wrapper. FocusPath={InputRouter.LastPathDebug}");
    }

    /// <summary>
    /// Reproduction test matching the exact WindowingDemo widget tree, including
    /// .Background(), .Unbounded(), .Fill() and NotificationPanel wrapping.
    /// Verifies that a window opened via menu receives focus and can be closed with Escape.
    /// </summary>
    [Fact]
    public async Task Window_ExactDemoTree_ReceivesFocusAndClosesWithEscape()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var windowClosed = false;
        string? focusPathAtEscape = null;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.NotificationPanel(outer.VStack(main => [
                        main.MenuBar(m => [
                            m.Menu("File", menu => [
                                menu.MenuItem("New Window").OnActivated(e =>
                                {
                                    var handle = e.Windows.Window(w => w.VStack(v => [
                                            v.Text(""),
                                            v.Text("  This is Window #1"),
                                            v.Text(""),
                                            v.Text("  Press Escape to close"),
                                            v.Text(""),
                                            v.HStack(h => [
                                                h.Text("  "),
                                                h.Button("Action").OnClick(_ => {}),
                                                h.Text(" "),
                                                h.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                                            ])
                                        ]))
                                        .Title("Window 1")
                                        .Size(45, 12)
                                        .OnClose(() => windowClosed = true);
                                    e.Windows.Open(handle);
                                })
                            ])
                        ]),
                        main.Text("Main content area"),
                        main.WindowPanel()
                            .Background(b => b.Text("background"))
                            .Unbounded().Fill()
                    ])).Fill()
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window #1"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => windowClosed, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.True(windowClosed,
            $"Window should have been closed by ESC. FocusPathBeforeEsc={focusPathAtEscape}, FocusPathAfterEsc={InputRouter.LastPathDebug}");
    }

    /// <summary>
    /// End-to-end test proving that opening a window via menu gives focus to
    /// its first focusable child (TextBox), so typing works immediately.
    /// Also proves Escape closes the window.
    /// </summary>
    [Fact]
    public async Task Window_OpenedViaMenu_TextBoxReceivesFocus_CanTypeImmediately()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var windowOpen = false;
        var typedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New Window").OnActivated(e =>
                            {
                                var handle = e.Windows.Window(w =>
                                        w.TextBox(typedText).OnTextChanged(args =>
                                        {
                                            typedText = args.NewText;
                                            return Task.CompletedTask;
                                        }))
                                    .Title("Input Window")
                                    .Size(40, 10)
                                    .Position(new WindowPositionSpec(WindowPosition.Center))
                                    .OnClose(() => { windowOpen = false; });
                                e.Windows.Open(handle);
                                windowOpen = true;
                            })
                        ])
                    ]),
                    outer.WindowPanel()
                        .Height(SizeHint.Fill),
                    outer.Text("Status bar")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open window via menu: Enter on File menu, Enter on New Window
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Input Window"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(windowOpen, "Window should be open");

        // Type immediately — if focus is correct, text should appear in the TextBox
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("Hello", typedText);

        // Escape closes the window
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !windowOpen, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.False(windowOpen, "Window should have been closed by Escape");
    }

    /// <summary>
    /// When two windows are open and the top one is closed, focus should
    /// return to the underlying window's first content focusable.
    /// </summary>
    [Fact]
    public async Task ClosingTopWindow_FocusReturnsToUnderlyingWindow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var textA = "";
        var textB = "";
        var windowAOpen = false;
        var windowBOpen = false;
        var windowCount = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New Window").OnActivated(e =>
                            {
                                windowCount++;
                                var num = windowCount;
                                var handle = e.Windows.Window(w =>
                                        w.TextBox(num == 1 ? textA : textB)
                                            .OnTextChanged(args =>
                                            {
                                                if (num == 1) textA = args.NewText;
                                                else textB = args.NewText;
                                                return Task.CompletedTask;
                                            }))
                                    .Title($"Window {num}")
                                    .Size(40, 10)
                                    .Position(new WindowPositionSpec(WindowPosition.Center))
                                    .OnClose(() =>
                                    {
                                        if (num == 1) windowAOpen = false;
                                        else windowBOpen = false;
                                    });
                                e.Windows.Open(handle);
                                if (num == 1) windowAOpen = true;
                                else windowBOpen = true;
                            })
                        ])
                    ]),
                    outer.WindowPanel()
                        .Height(SizeHint.Fill),
                    outer.Text("Status")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open Window A via menu
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window 1"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(windowAOpen);

        // Type into Window A to prove it has focus
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("AAA")
            .WaitUntil(s => s.ContainsText("AAA"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("AAA", textA);

        // Open Window B via menu (click to avoid keyboard focus issues)
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 0)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window 2"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(windowBOpen);

        // Type into Window B to prove it has focus
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("BBB")
            .WaitUntil(s => s.ContainsText("BBB"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("BBB", textB);

        // Close Window B with Escape — focus should return to Window A
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !windowBOpen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(windowBOpen);
        Assert.True(windowAOpen);

        // Type more — should go into Window A (focus restored)
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("X")
            .WaitUntil(_ => textA == "AAAX", TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("AAAX", textA);
    }

    /// <summary>
    /// Tests cascade close: open 3 windows, close each in turn, focus returns
    /// to the next one in z-order each time.
    /// </summary>
    [Fact]
    public async Task CascadeClose_ThreeWindows_FocusFollowsZOrder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var texts = new[] { "", "", "" };
        var windowOpen = new[] { false, false, false };
        var windowCount = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New").OnActivated(e =>
                            {
                                var idx = windowCount++;
                                var handle = e.Windows.Window(w =>
                                        w.TextBox(texts[idx])
                                            .OnTextChanged(args =>
                                            {
                                                texts[idx] = args.NewText;
                                                return Task.CompletedTask;
                                            }))
                                    .Title($"Win{idx + 1}")
                                    .Size(30, 8)
                                    .OnClose(() => windowOpen[idx] = false);
                                e.Windows.Open(handle);
                                windowOpen[idx] = true;
                            })
                        ])
                    ]),
                    outer.WindowPanel().Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open 3 windows in sequence
        for (int i = 0; i < 3; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
                .ClickAt(2, 0)
                .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(5))
                .Key(Hex1bKey.Enter)
                .WaitUntil(s => s.ContainsText($"Win{i + 1}"), TimeSpan.FromSeconds(5))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        }

        Assert.True(windowOpen[0] && windowOpen[1] && windowOpen[2]);

        // Close Win3 → focus should go to Win2
        // Wait for visual state change (Win3 title gone) to ensure render loop has
        // processed the close and RequestFocusCallback has set focus on Win2.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !windowOpen[2] && !s.ContainsText("Win3"), TimeSpan.FromSeconds(5))
            .Type("2")
            .WaitUntil(_ => texts[1] == "2", TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("2", texts[1]);

        // Close Win2 → focus should go to Win1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => !windowOpen[1] && !s.ContainsText("Win2"), TimeSpan.FromSeconds(5))
            .Type("1")
            .WaitUntil(_ => texts[0] == "1", TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("1", texts[0]);
    }

    /// <summary>
    /// When the last window is closed, focus should return to the background
    /// (e.g., MenuBar or first focusable in the main content).
    /// </summary>
    [Fact]
    public async Task ClosingLastWindow_FocusReturnsToBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var windowOpen = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("New").OnActivated(e =>
                            {
                                var handle = e.Windows.Window(w => w.Button("In Window").OnClick(_ => {}))
                                    .Title("Solo")
                                    .Size(30, 8)
                                    .OnClose(() => windowOpen = false);
                                e.Windows.Open(handle);
                                windowOpen = true;
                            })
                        ])
                    ]),
                    outer.WindowPanel().Height(SizeHint.Fill),
                    outer.Text("Status")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open window via menu (use click to avoid focus-on-textbox issue)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .ClickAt(2, 0)
            .WaitUntil(s => s.ContainsText("New"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Solo"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(windowOpen);

        // Close with Escape — last window, app should not hang
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !windowOpen, TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.False(windowOpen);
    }

    /// <summary>
    /// Closing a modal window returns focus to the non-modal window underneath.
    /// </summary>
    [Fact]
    public async Task ClosingModalWindow_FocusReturnsToNonModal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        var baseText = "";
        var baseWindowOpen = false;
        var modalOpen = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("File", menu => [
                            menu.MenuItem("Open Base").OnActivated(e =>
                            {
                                var handle = e.Windows.Window(w =>
                                        w.TextBox(baseText)
                                            .OnTextChanged(args =>
                                            {
                                                baseText = args.NewText;
                                                return Task.CompletedTask;
                                            }))
                                    .Title("Base Window")
                                    .Size(40, 10)
                                    .OnClose(() => baseWindowOpen = false);
                                e.Windows.Open(handle);
                                baseWindowOpen = true;
                            }),
                            menu.MenuItem("Open Modal").OnActivated(e =>
                            {
                                var handle = e.Windows.Window(mw =>
                                        mw.Button("OK").OnClick(_ => {}))
                                    .Title("Confirm")
                                    .Size(25, 6)
                                    .Modal()
                                    .OnClose(() => modalOpen = false);
                                e.Windows.Open(handle);
                                modalOpen = true;
                            })
                        ])
                    ]),
                    outer.WindowPanel().Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open base window
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(5))
            .ClickAt(2, 0)
            .WaitUntil(s => s.ContainsText("Open Base"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Base Window"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(baseWindowOpen);

        // Type to prove base window has focus
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("AB")
            .WaitUntil(_ => baseText == "AB", TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Open modal via menu
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 0)
            .WaitUntil(s => s.ContainsText("Open Modal"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Confirm"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(modalOpen);

        // Close modal with Escape
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !modalOpen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.False(modalOpen);
        Assert.True(baseWindowOpen);

        // Type into base window — should work if focus was restored
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("CD")
            .WaitUntil(_ => baseText == "ABCD", TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("ABCD", baseText);
    }
}
