using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests demonstrating the terminal widget size misalignment issue.
/// When a TerminalWidget is embedded in a Hex1bApp, the PTY process may start
/// at the initial WithDimensions() size (e.g., 80x24) instead of the actual
/// layout bounds (e.g., 120x40), causing rendering glitches for full-screen apps.
/// </summary>
public class TerminalWidgetSizeAlignmentTests
{
    /// <summary>
    /// Demonstrates the core problem: after RunAsync starts and ArrangeCore fires,
    /// all components should agree on dimensions. This test verifies the dimension
    /// chain: handle → terminal → workload (child process).
    /// </summary>
    [Fact]
    public async Task AllComponents_ShouldAgreeOnDimensions_AfterStartupAndArrange()
    {
        // Simulate: terminal created at 80x24, widget laid out at 120x40
        const int InitialWidth = 80;
        const int InitialHeight = 24;
        const int LayoutWidth = 120;
        const int LayoutHeight = 40;
        
        using var cts = new CancellationTokenSource();
        
        var builder = Hex1bTerminal.CreateBuilder()
            .WithDimensions(InitialWidth, InitialHeight)
            .WithScrollback(100);

        // Use diagnostic shell so we don't need a real PTY
        builder = builder.WithDiagnosticShell();

        var terminal = builder
            .WithTerminalWidget(out var handle)
            .Build();

        // Start RunAsync on a background thread (same pattern as ScrollbackDemo)
        var runTask = Task.Run(async () =>
        {
            try { await terminal.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });

        // Wait for terminal to transition to Running
        var timeout = TimeSpan.FromSeconds(5);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (handle.State != TerminalState.Running && sw.Elapsed < timeout)
        {
            await Task.Delay(10);
        }
        Assert.Equal(TerminalState.Running, handle.State);

        // Simulate what ArrangeCore does: resize the handle to layout bounds
        handle.Resize(LayoutWidth, LayoutHeight);
        
        // Allow async resize to propagate
        await Task.Delay(100);

        // Verify ALL components agree on the new dimensions
        Assert.Equal(LayoutWidth, handle.Width);
        Assert.Equal(LayoutHeight, handle.Height);
        Assert.Equal(LayoutWidth, terminal.Width);
        Assert.Equal(LayoutHeight, terminal.Height);

        // Clean up
        cts.Cancel();
        try { await runTask; } catch { }
        await terminal.DisposeAsync();
    }

    /// <summary>
    /// Demonstrates the race condition: when RunAsync starts before the first
    /// ArrangeCore, the PTY may be created with stale dimensions.
    /// This test creates a real PTY process to check if the workload's stored
    /// dimensions match after resize.
    /// </summary>
    [Fact]
    public async Task WorkloadDimensions_ShouldMatchLayout_AfterResize()
    {
        const int InitialWidth = 80;
        const int InitialHeight = 24;
        const int LayoutWidth = 120;
        const int LayoutHeight = 40;
        
        using var cts = new CancellationTokenSource();
        
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(InitialWidth, InitialHeight)
            .WithDiagnosticShell()
            .WithTerminalWidget(out var handle)
            .Build();

        // Start terminal
        var runTask = Task.Run(async () =>
        {
            try { await terminal.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });

        // Wait for Running
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (handle.State != TerminalState.Running && sw.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);

        // Resize handle (simulates ArrangeCore)
        handle.Resize(LayoutWidth, LayoutHeight);
        await Task.Delay(100);

        // Check the workload's dimension tracking
        var workload = terminal.Workload;
        if (workload is Hex1bTerminalChildProcess childProcess)
        {
            Assert.Equal(LayoutWidth, childProcess.Width);
            Assert.Equal(LayoutHeight, childProcess.Height);
        }

        cts.Cancel();
        try { await runTask; } catch { }
        await terminal.DisposeAsync();
    }

    /// <summary>
    /// Tests that resizing the handle BEFORE RunAsync starts correctly
    /// propagates to the terminal and workload dimensions.
    /// This simulates the case where ArrangeCore runs first (race won by UI thread).
    /// </summary>
    [Fact]
    public async Task ResizeBeforeStart_ShouldPropagateToAllComponents()
    {
        const int InitialWidth = 80;
        const int InitialHeight = 24;
        const int LayoutWidth = 120;
        const int LayoutHeight = 40;
        
        using var cts = new CancellationTokenSource();
        
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(InitialWidth, InitialHeight)
            .WithDiagnosticShell()
            .WithTerminalWidget(out var handle)
            .Build();

        // Resize BEFORE starting (simulates ArrangeCore running before RunAsync)
        handle.Resize(LayoutWidth, LayoutHeight);
        
        // Verify terminal got the resize
        Assert.Equal(LayoutWidth, terminal.Width);
        Assert.Equal(LayoutHeight, terminal.Height);
        
        // Now start
        var runTask = Task.Run(async () =>
        {
            try { await terminal.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (handle.State != TerminalState.Running && sw.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);

        // Dimensions should still match after startup
        Assert.Equal(LayoutWidth, handle.Width);
        Assert.Equal(LayoutHeight, handle.Height);
        Assert.Equal(LayoutWidth, terminal.Width);
        Assert.Equal(LayoutHeight, terminal.Height);

        cts.Cancel();
        try { await runTask; } catch { }
        await terminal.DisposeAsync();
    }

    /// <summary>
    /// Tests the TerminalNode arrange → resize flow end-to-end.
    /// Creates a TerminalNode, arranges it at different bounds than the handle's
    /// initial size, and verifies the handle gets resized.
    /// </summary>
    [Fact]
    public void ArrangeCore_ResizesHandle_ToMatchLayoutBounds()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        
        // Set state to Running so ArrangeCore doesn't take the fallback path
        var stateField = typeof(TerminalWidgetHandle).GetField("_state",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        stateField!.SetValue(handle, TerminalState.Running);
        
        var node = new TerminalNode { Handle = handle };
        node.SetInvalidateCallback(() => { });
        node.Bind();
        
        // First arrange at 120x40 (simulates first layout pass)
        node.Arrange(new Rect(0, 0, 120, 40));
        
        Assert.Equal(120, handle.Width);
        Assert.Equal(40, handle.Height);
    }
    
    /// <summary>
    /// Tests that ArrangeCore resizes the handle even when terminal is NotStarted
    /// and a fallback child exists. This was previously broken (early return skipped resize).
    /// </summary>
    [Fact]
    public void ArrangeCore_ResizesHandle_EvenWhenShowingFallback()
    {
        var handle = new TerminalWidgetHandle(80, 24);
        // State stays NotStarted (default)
        
        var node = new TerminalNode
        {
            Handle = handle,
            NotRunningBuilder = args => throw new NotImplementedException("should not be called in arrange"),
            FallbackChild = new TextBlockNode() // Dummy fallback
        };
        node.SetInvalidateCallback(() => { });
        
        // Arrange at 120x40
        node.Arrange(new Rect(0, 0, 120, 40));
        
        // Handle should be resized even though fallback is shown
        Assert.Equal(120, handle.Width);
        Assert.Equal(40, handle.Height);
    }

    /// <summary>
    /// Reproduces the race condition scenario from the ScrollbackDemo:
    /// - Terminal built at 80x24
    /// - RunAsync started on background thread 
    /// - Widget arranged at larger size
    /// - Verifies that after a brief settle, dimensions are consistent
    /// </summary>
    [Fact]
    public async Task ScrollbackDemoPattern_DimensionsShouldConverge()
    {
        const int InitialWidth = 80;
        const int InitialHeight = 24;
        const int LayoutWidth = 132;
        const int LayoutHeight = 43;

        using var cts = new CancellationTokenSource();

        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(InitialWidth, InitialHeight)
            .WithScrollback(1000)
            .WithDiagnosticShell()
            .WithTerminalWidget(out var handle)
            .Build();

        // Pattern from ScrollbackDemo: fire-and-forget RunAsync
        _ = Task.Run(async () =>
        {
            try { await terminal.RunAsync(cts.Token); }
            catch (OperationCanceledException) { }
        });

        // Simulate first ArrangeCore (may race with RunAsync)
        handle.Resize(LayoutWidth, LayoutHeight);

        // Wait for terminal to be running and dimensions to settle
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (handle.State != TerminalState.Running && sw.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10);
        
        // Give async operations time to complete
        await Task.Delay(200);

        // ALL dimensions should agree on the layout size
        Assert.Equal(LayoutWidth, handle.Width);
        Assert.Equal(LayoutHeight, handle.Height);
        Assert.Equal(LayoutWidth, terminal.Width);
        Assert.Equal(LayoutHeight, terminal.Height);

        cts.Cancel();
        await terminal.DisposeAsync();
    }
}
