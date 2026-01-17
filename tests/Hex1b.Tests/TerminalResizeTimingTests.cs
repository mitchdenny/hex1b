using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for verifying terminal resize timing behavior.
/// These tests focus on the race condition where resize events occur
/// before the PTY process has started.
/// </summary>
public class TerminalResizeTimingTests
{
    /// <summary>
    /// Verifies that when ResizeAsync is called before StartAsync,
    /// the PTY still starts with the correct dimensions.
    /// </summary>
    [Fact]
    public async Task ResizeAsync_CalledBeforeStart_PtyStartsWithUpdatedDimensions()
    {
        // Arrange
        // Create a child terminal but don't start it yet
        var childProcess = new Hex1bTerminalChildProcess(
            "bash", ["--norc"],
            initialWidth: 80,
            initialHeight: 24);
        
        // Act - Resize before start (simulates what happens when Arrange is called
        // before RunAsync completes)
        await childProcess.ResizeAsync(148, 36);
        
        // Start the process
        await childProcess.StartAsync(TestContext.Current.CancellationToken);
        
        // Give bash time to output its prompt
        await Task.Delay(500, TestContext.Current.CancellationToken);
        
        // Assert - Check the process dimensions
        Assert.Equal(148, childProcess.Width);
        Assert.Equal(36, childProcess.Height);
        
        // Clean up
        childProcess.Kill();
        await childProcess.DisposeAsync();
    }
    
    /// <summary>
    /// Verifies that TerminalWidgetHandle.Resize fires the Resized event
    /// which should propagate to the Hex1bTerminal.
    /// </summary>
    [Fact]
    public void TerminalWidgetHandle_Resize_FiresResizedEvent()
    {
        // Arrange
        var handle = new TerminalWidgetHandle(80, 24);
        int resizedCount = 0;
        int resizedWidth = 0;
        int resizedHeight = 0;
        
        handle.Resized += (w, h) =>
        {
            resizedCount++;
            resizedWidth = w;
            resizedHeight = h;
        };
        
        // Act
        handle.Resize(148, 36);
        
        // Assert
        Assert.Equal(1, resizedCount);
        Assert.Equal(148, resizedWidth);
        Assert.Equal(36, resizedHeight);
        Assert.Equal(148, handle.Width);
        Assert.Equal(36, handle.Height);
    }
    
    /// <summary>
    /// Verifies the complete flow: TerminalNode.Arrange resizes handle,
    /// which should propagate through Hex1bTerminal to the PTY.
    /// </summary>
    [Fact]
    public async Task TerminalNode_Arrange_PropagatesResizeToPty()
    {
        // Arrange - Create terminal with widget handle
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)  // Initial smaller size
            .WithPtyProcess("bash", "--norc")
            .WithTerminalWidget(out var handle)
            .Build();
        
        var node = new TerminalNode { Handle = handle };
        
        // Track resize events
        int resizeCount = 0;
        handle.Resized += (w, h) => resizeCount++;
        
        // Act - Start terminal in background
        var runTask = Task.Run(() => terminal.RunAsync(TestContext.Current.CancellationToken));
        
        // Wait for PTY to start
        await Task.Delay(200, TestContext.Current.CancellationToken);
        
        // Simulate what happens when the widget is arranged in a larger container
        // First measure with constraints
        node.Measure(new Constraints(0, 148, 0, 36));
        
        // Then arrange with the final bounds
        node.Arrange(new Rect(0, 0, 148, 36));
        
        // Wait a bit for resize to propagate
        await Task.Delay(300, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.True(resizeCount > 0, "Resize event should have fired");
        Assert.Equal(148, handle.Width);
        Assert.Equal(36, handle.Height);
        
        // Clean up
        await terminal.DisposeAsync();
    }
    
    /// <summary>
    /// Tests that output from a terminal is received even after handle resize.
    /// This simulates the scenario where a second terminal is created and resized.
    /// </summary>
    [Fact]
    public async Task Terminal_AfterResize_ReceivesOutput()
    {
        // Arrange
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithPtyProcess("bash", "--norc")
            .WithTerminalWidget(out var handle)
            .Build();
        
        bool outputReceived = false;
        handle.OutputReceived += () => outputReceived = true;
        
        // Act - Start terminal
        var runTask = Task.Run(() => terminal.RunAsync(TestContext.Current.CancellationToken));
        
        // Wait for PTY to start and output something
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        
        // Resize (simulates what TerminalNode.Arrange does)
        handle.Resize(148, 36);
        
        // Wait for any additional output after resize
        await Task.Delay(500, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.True(outputReceived, "Should have received output from bash");
        
        // Check that the buffer has content (bash prompt)
        var buffer = handle.GetScreenBuffer();
        bool hasContent = false;
        for (int y = 0; y < handle.Height && !hasContent; y++)
        {
            for (int x = 0; x < handle.Width && !hasContent; x++)
            {
                if (!string.IsNullOrWhiteSpace(buffer[y, x].Character))
                {
                    hasContent = true;
                }
            }
        }
        
        Assert.True(hasContent, "Buffer should contain bash prompt");
        
        // Clean up
        await terminal.DisposeAsync();
    }
    
    /// <summary>
    /// Tests that TerminalNode correctly subscribes to OutputReceived
    /// and marks itself dirty when output arrives.
    /// </summary>
    [Fact]
    public async Task TerminalNode_WhenBound_ReceivesOutputAndMarksDirty()
    {
        // Arrange
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(80, 24)
            .WithPtyProcess("bash", "--norc")
            .WithTerminalWidget(out var handle)
            .Build();
        
        var node = new TerminalNode { Handle = handle };
        bool invalidateCalled = false;
        
        // Simulate what Hex1bApp does during reconciliation
        node.SetInvalidateCallback(() => invalidateCalled = true);
        node.Bind();
        
        // Act - Start terminal
        var runTask = Task.Run(() => terminal.RunAsync(TestContext.Current.CancellationToken));
        
        // Wait for bash to output its prompt
        await Task.Delay(2000, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.True(invalidateCalled, "Invalidate callback should have been called when output arrived");
        Assert.True(node.HasPendingOutput || handle.State == TerminalState.Running, 
            "Node should have pending output or terminal should be running");
        
        // Clean up
        node.Unbind();
        await terminal.DisposeAsync();
    }
    
    /// <summary>
    /// This test simulates the exact scenario of switching between terminals
    /// to verify the second terminal receives output correctly.
    /// </summary>
    [Fact]
    public async Task SecondTerminal_WhenSwitchedTo_ReceivesAndDisplaysOutput()
    {
        // Arrange - Create two terminals
        var terminal1 = Hex1bTerminal.CreateBuilder()
            .WithDimensions(148, 36)
            .WithPtyProcess("bash", "--norc")
            .WithTerminalWidget(out var handle1)
            .Build();
        
        var terminal2 = Hex1bTerminal.CreateBuilder()
            .WithDimensions(148, 36)
            .WithPtyProcess("bash", "--norc")
            .WithTerminalWidget(out var handle2)
            .Build();
        
        // Create nodes for each
        var node = new TerminalNode();
        bool invalidateCalled = false;
        node.SetInvalidateCallback(() => invalidateCalled = true);
        
        // Act - Start first terminal and bind node to it
        var runTask1 = Task.Run(() => terminal1.RunAsync(TestContext.Current.CancellationToken));
        node.Handle = handle1;
        node.Bind();
        
        // Wait for first terminal to output
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        
        // Verify first terminal works
        var buffer1 = handle1.GetScreenBuffer();
        bool terminal1HasContent = HasNonEmptyContent(buffer1, handle1.Height, handle1.Width);
        Assert.True(terminal1HasContent, "First terminal should have content");
        
        // Now switch to second terminal (simulates what reconciliation does)
        node.Unbind();
        
        // Start second terminal
        var runTask2 = Task.Run(() => terminal2.RunAsync(TestContext.Current.CancellationToken));
        
        // Bind to second terminal
        invalidateCalled = false;
        node.Handle = handle2;
        node.Bind();
        
        // Arrange the node (simulates layout phase)
        node.Measure(new Constraints(0, 148, 0, 36));
        node.Arrange(new Rect(0, 0, 148, 36));
        
        // Wait for second terminal to output
        await Task.Delay(2000, TestContext.Current.CancellationToken);
        
        // Assert - Second terminal should have content
        var buffer2 = handle2.GetScreenBuffer();
        bool terminal2HasContent = HasNonEmptyContent(buffer2, handle2.Height, handle2.Width);
        
        Assert.True(terminal2HasContent, 
            $"Second terminal should have content. State={handle2.State}, Width={handle2.Width}, Height={handle2.Height}");
        Assert.True(invalidateCalled, "Invalidate should have been called for second terminal");
        
        // Clean up
        node.Unbind();
        await terminal1.DisposeAsync();
        await terminal2.DisposeAsync();
    }
    
    private static bool HasNonEmptyContent(TerminalCell[,] buffer, int height, int width)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!string.IsNullOrWhiteSpace(buffer[y, x].Character))
                {
                    return true;
                }
            }
        }
        return false;
    }
    
    /// <summary>
    /// This test simulates the EXACT scenario in EmbeddedTerminalDemo where
    /// terminals are created within a Hex1bApp and switched via UI.
    /// </summary>
    [Fact]
    public async Task FullIntegration_SecondTerminal_ShowsOutput()
    {
        // This test uses a headless Hex1bApp with embedded terminals
        using var workload = new Hex1bAppWorkloadAdapter();
        var terminalOptions = new Hex1bTerminalOptions
        {
            Width = 150,
            Height = 40,
            WorkloadAdapter = workload
        };
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        // State
        var terminals = new List<(int id, Hex1bTerminal term, TerminalWidgetHandle handle)>();
        var terminalLock = new object();
        var nextTerminalId = 1;
        var activeTerminalId = 0;
        Hex1bApp? appRef = null;
        
        void AddTerminal()
        {
            var id = nextTerminalId++;
            var childTerminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(148, 36)
                .WithPtyProcess("bash", "--norc")
                .WithTerminalWidget(out var handle)
                .Build();
            
            lock (terminalLock)
            {
                terminals.Add((id, childTerminal, handle));
                activeTerminalId = id;
            }
            
            // Start the terminal in background
            _ = Task.Run(async () =>
            {
                try { await childTerminal.RunAsync(TestContext.Current.CancellationToken); }
                catch (OperationCanceledException) { }
            });
            
            // Invalidate to trigger re-render with the new terminal
            appRef?.Invalidate();
        }
        
        Hex1bWidget BuildUI(RootContext ctx)
        {
            List<(int id, Hex1bTerminal term, TerminalWidgetHandle handle)> currentTerminals;
            lock (terminalLock)
            {
                currentTerminals = [.. terminals];
            }
            
            if (currentTerminals.Count == 0)
            {
                return ctx.Text("No terminals");
            }
            
            var activeSession = currentTerminals.FirstOrDefault(s => s.id == activeTerminalId);
            if (activeSession.handle == null)
            {
                activeSession = currentTerminals[0];
            }
            
            return ctx.Border(
                ctx.Terminal(activeSession.handle).Fill(),
                title: $"Terminal {activeSession.id}"
            ).Fill();
        }
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult(BuildUI(ctx)),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );
        appRef = app;
        
        // Run app in background
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        try
        {
            // Wait for app to start
            await Task.Delay(200, TestContext.Current.CancellationToken);
            
            // Create first terminal
            AddTerminal();
            
            // Wait for first terminal to output its prompt
            await Task.Delay(2000, TestContext.Current.CancellationToken);
            
            // Verify first terminal has content
            var handle1 = terminals[0].handle;
            var buffer1 = handle1.GetScreenBuffer();
            bool terminal1HasContent = HasNonEmptyContent(buffer1, handle1.Height, handle1.Width);
            Assert.True(terminal1HasContent, "First terminal should have content");
            
            // Create second terminal
            AddTerminal();
            
            // Wait for second terminal to output its prompt
            await Task.Delay(3000, TestContext.Current.CancellationToken);
            
            // Verify second terminal has content
            var handle2 = terminals[1].handle;
            var buffer2 = handle2.GetScreenBuffer();
            bool terminal2HasContent = HasNonEmptyContent(buffer2, handle2.Height, handle2.Width);
            
            // Also check the handle's state
            TestContext.Current.SendDiagnosticMessage($"Terminal 2 state: {handle2.State}");
            TestContext.Current.SendDiagnosticMessage($"Terminal 2 dimensions: {handle2.Width}x{handle2.Height}");
            
            Assert.True(terminal2HasContent, 
                $"Second terminal should have content. State={handle2.State}, " +
                $"Width={handle2.Width}, Height={handle2.Height}");
        }
        finally
        {
            appRef.RequestStop();
            
            // Clean up child terminals
            foreach (var (_, term, _) in terminals)
            {
                await term.DisposeAsync();
            }
        }
        
        await runTask;
    }
}
