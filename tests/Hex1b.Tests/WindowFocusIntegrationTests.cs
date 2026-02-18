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
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(10))
            .Alt().Key(Hex1bKey.F)  // ALT-F to open File menu
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify: Menu opened (New Window is visible)
        Assert.Equal(0, menuItemActivatedCount); // Menu just opened, nothing activated yet
        
        // Step 2: Press Enter to activate New Window (first item is focused)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)  // Activate "New Window"
            .WaitUntil(s => s.ContainsText("Test Window"), TimeSpan.FromSeconds(1))
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
            .WaitUntil(s => s.ContainsText("Exit"), TimeSpan.FromSeconds(1), "Waiting for Exit to be visible")
            .Key(Hex1bKey.Enter)  // Activate Exit
            .WaitUntil(_ => menuItemActivatedCount == 2, TimeSpan.FromSeconds(1), "Waiting for Exit activation")
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
    /// </summary>
    [Fact(Skip = "Focus management changed - test needs update for new WindowPanel behavior")]
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
                                var handle = e.Windows.Window(w => w.Text("Content"))
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
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(10))
            .Alt().Key(Hex1bKey.F)
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(1))
            .Key(Hex1bKey.Enter)  // Activate New Window menu item
            .WaitUntil(s => s.ContainsText("Window"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Now try to use ALT-F to open menu
        await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.F)
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Empty(activatedItems); // Menu opened but nothing activated yet

        // Navigate down twice (to Item 3) and activate
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(100))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Verify: Item 3 was activated (proves arrow navigation worked)
        Assert.Single(activatedItems);
        Assert.Equal("Item 3", activatedItems[0]);
    }



    [Fact(Skip = "Focus management changed - WindowPanel no longer has Content, focus doesn't auto-transfer from sibling widgets")]
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
            .WaitUntil(s => s.ContainsText("Open Both Windows"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)  // Click button to open both windows
            .WaitUntil(s => s.ContainsText("Window 1") && s.ContainsText("Window 2"), TimeSpan.FromSeconds(1))
            .Capture("after_open")
            // Window 2 is active (opened last), so ESC should close it
            .Key(Hex1bKey.Escape)
            // Just wait a bit for the escape to be processed, then exit
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(200))
            .Capture("after_escape")
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
            .WaitUntil(s => s.ContainsText("Open Both"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Window 2"), TimeSpan.FromSeconds(1))
            .Capture("both_windows")
            // Window 2 is on top and should be active
            // Click on Window 1 (at position 5, 3 which is inside window 1 but not inside window 2)
            .ClickAt(5, 3)
            .WaitUntil(s => true, TimeSpan.FromMilliseconds(100)) // Brief wait for click to process
            .Capture("after_click_window1")
            // Now press Escape - should close Window 1 (the newly focused one)
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => window1Closed || window2Closed, TimeSpan.FromSeconds(1))
            .Capture("after_escape")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Window 1 should be closed (we clicked on it), Window 2 should still be open
        Assert.True(window1Closed, "Window 1 should have been closed by ESC (we clicked on it to focus it)");
        Assert.False(window2Closed, "Window 2 should NOT have been closed");
    }

    [Fact(Skip = "Focus management changed - WindowPanel no longer has Content, focus doesn't auto-transfer from sibling widgets")]
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
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(10))
            // Open File menu and select New Window (opens window 1)
            .Key(Hex1bKey.Enter)  // Opens File menu
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(1))
            .Key(Hex1bKey.Enter)  // Activates New Window
            .WaitUntil(s => s.ContainsText("Window 1"), TimeSpan.FromSeconds(1))
            // Click on the menu bar to open menu again
            .ClickAt(2, 0)  // Click File menu
            .WaitUntil(s => s.ContainsText("New Window"), TimeSpan.FromSeconds(1))
            .Key(Hex1bKey.Enter)  // Activates New Window (opens window 2)
            .WaitUntil(s => s.ContainsText("Window 2"), TimeSpan.FromSeconds(1))
            // Now press Escape - should close window 2 (the active one)
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => window1Closed || window2Closed, TimeSpan.FromSeconds(1))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var focusPath = InputRouter.LastPathDebug;
        
        // Window 2 should be closed (it was active), Window 1 should still be open
        Assert.True(window2Closed, $"Window 2 should have been closed by ESC. W1Closed={window1Closed}, W2Closed={window2Closed}. FocusPath={focusPath}");
        Assert.False(window1Closed, $"Window 1 should NOT have been closed. FocusPath={focusPath}");
    }
}
