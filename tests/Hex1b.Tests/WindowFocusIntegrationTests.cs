using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for window focus behavior.
/// </summary>
public class WindowFocusIntegrationTests
{
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
                new WindowPanelWidget(
                    new VStackWidget([
                        new ButtonWidget("Open Both Windows").OnClick(e =>
                        {
                            // Offset windows so both are visible
                            e.Windows.Open(
                                id: "window-1",
                                title: "Window 1",
                                content: () => new ButtonWidget("W1 Button").OnClick(_ => {}),
                                width: 30,
                                height: 8,
                                position: new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 2, OffsetY: 2),
                                onClose: () => window1Closed = true
                            );
                            e.Windows.Open(
                                id: "window-2",
                                title: "Window 2",
                                content: () => new ButtonWidget("W2 Button").OnClick(_ => {}),
                                width: 30,
                                height: 8,
                                position: new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 35, OffsetY: 2),
                                onClose: () => window2Closed = true
                            );
                        })
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open both windows at once, then press ESC
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open Both Windows"), TimeSpan.FromSeconds(2))
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
                new WindowPanelWidget(
                    new VStackWidget([
                        new ButtonWidget("Open Both").OnClick(e =>
                        {
                            e.Windows.Open(
                                id: "window-1",
                                title: "Window 1",
                                content: () => new TextBlockWidget("Content 1"),
                                width: 30,
                                height: 10,
                                position: new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 2, OffsetY: 2),
                                onClose: () => window1Closed = true
                            );
                            e.Windows.Open(
                                id: "window-2",
                                title: "Window 2",
                                content: () => new TextBlockWidget("Content 2"),
                                width: 30,
                                height: 10,
                                position: new WindowPositionSpec(WindowPosition.TopLeft, OffsetX: 10, OffsetY: 5),
                                onClose: () => window2Closed = true
                            );
                        })
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open both windows
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Open Both"), TimeSpan.FromSeconds(2))
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
                new WindowPanelWidget(
                    new VStackWidget([
                        new MenuBarWidget([
                            new MenuWidget("File", [
                                new MenuItemWidget("New Window").OnActivated(e =>
                                {
                                    windowCounter++;
                                    var num = windowCounter;
                                    e.Windows.Open(
                                        id: $"window-{num}",
                                        title: $"Window {num}",
                                        content: () => new ButtonWidget($"Content {num}").OnClick(btn => 
                                        {
                                            focusedBeforeEsc = $"Window {num} button clicked";
                                        }),
                                        width: 30,
                                        height: 10,
                                        onClose: () => 
                                        { 
                                            if (num == 1) window1Closed = true; 
                                            else window2Closed = true; 
                                        }
                                    );
                                })
                            ])
                        ]),
                        new TextBlockWidget("Main content area")
                    ])
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open two windows via menu
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("File"), TimeSpan.FromSeconds(2))
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
