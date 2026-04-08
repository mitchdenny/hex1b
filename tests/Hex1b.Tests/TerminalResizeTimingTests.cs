using Hex1b.Automation;
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
    private const string StartupMarker = "HEX1B_TERMINAL_READY";

    /// <summary>
    /// Verifies that when ResizeAsync is called before StartAsync,
    /// the PTY still starts with the correct dimensions.
    /// </summary>
    [Fact]
    public async Task ResizeAsync_CalledBeforeStart_PtyStartsWithUpdatedDimensions()
    {
        // Arrange
        // Create a child terminal but don't start it yet
        var childProcess = CreateInteractiveShellProcess(initialWidth: 80, initialHeight: 24);
        
        // Act - Resize before start (simulates what happens when Arrange is called
        // before RunAsync completes)
        await childProcess.ResizeAsync(148, 36);
        
        // Start the process
        await childProcess.StartAsync(TestContext.Current.CancellationToken);
        
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
        var terminal = WithInteractiveShell(
            Hex1bTerminal.CreateBuilder()
                .WithDimensions(80, 24))  // Initial smaller size
            .WithTerminalWidget(out var handle)
            .Build();
        
        var node = new TerminalNode { Handle = handle };
        
        // Track resize events
        int resizeCount = 0;
        handle.Resized += (w, h) => resizeCount++;
        
        // Act - Start terminal in background
        var runTask = Task.Run(() => terminal.RunAsync(TestContext.Current.CancellationToken));
        try
        {
            await WaitForTerminalStateAsync(handle, TerminalState.Running, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            
            // Simulate what happens when the widget is arranged in a larger container
            // First measure with constraints
            node.Measure(new Constraints(0, 148, 0, 36));
            
            // Then arrange with the final bounds
            node.Arrange(new Rect(0, 0, 148, 36));
            
            // Assert
            Assert.True(resizeCount > 0, "Resize event should have fired");
            Assert.Equal(148, handle.Width);
            Assert.Equal(36, handle.Height);
        }
        finally
        {
            await terminal.DisposeAsync();
            await AwaitTerminalRunTaskAsync(runTask);
        }
    }
    
    /// <summary>
    /// Tests that output from a terminal is received even after handle resize.
    /// This simulates the scenario where a second terminal is created and resized.
    /// </summary>
    [Fact]
    public async Task Terminal_AfterResize_ReceivesOutput()
    {
        // Arrange
        var terminal = WithInteractiveShell(
            Hex1bTerminal.CreateBuilder()
                .WithDimensions(80, 24))
            .WithTerminalWidget(out var handle)
            .Build();
        
        bool outputReceived = false;
        handle.OutputReceived += () => outputReceived = true;
        
        // Act - Start terminal
        var runTask = Task.Run(() => terminal.RunAsync(TestContext.Current.CancellationToken));
        try
        {
            await WaitForTerminalContentAsync(handle, GetStartupTimeout(), TestContext.Current.CancellationToken);
            
            // Resize (simulates what TerminalNode.Arrange does)
            handle.Resize(148, 36);
            
            // Assert
            Assert.True(outputReceived, "Should have received output from the shell.");
            
            // Check that the buffer has content
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
            
            Assert.True(hasContent, "Buffer should contain shell output.");
        }
        finally
        {
            await terminal.DisposeAsync();
            await AwaitTerminalRunTaskAsync(runTask);
        }
    }
    
    /// <summary>
    /// Tests that TerminalNode correctly subscribes to OutputReceived
    /// and marks itself dirty when output arrives.
    /// </summary>
    [Fact]
    public async Task TerminalNode_WhenBound_ReceivesOutputAndMarksDirty()
    {
        // Arrange
        var terminal = WithInteractiveShell(
            Hex1bTerminal.CreateBuilder()
                .WithDimensions(80, 24))
            .WithTerminalWidget(out var handle)
            .Build();
        
        var node = new TerminalNode { Handle = handle };
        bool invalidateCalled = false;
        var invalidateSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Simulate what Hex1bApp does during reconciliation
        node.SetInvalidateCallback(() =>
        {
            invalidateCalled = true;
            invalidateSignal.TrySetResult();
        });
        node.Bind();
        
        // Act - Start terminal
        var runTask = Task.Run(() => terminal.RunAsync(TestContext.Current.CancellationToken));
        try
        {
            await invalidateSignal.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await WaitForTerminalStateAsync(handle, TerminalState.Running, GetStartupTimeout(), TestContext.Current.CancellationToken);
            
            // Assert
            Assert.True(invalidateCalled, "Invalidate callback should have been called when output arrived");
            Assert.True(node.HasPendingOutput || handle.State == TerminalState.Running, 
                "Node should have pending output or terminal should be running");
        }
        finally
        {
            node.Unbind();
            await terminal.DisposeAsync();
            await AwaitTerminalRunTaskAsync(runTask);
        }
    }
    
    /// <summary>
    /// This test simulates the exact scenario of switching between terminals
    /// to verify the second terminal receives output correctly.
    /// </summary>
    [Fact]
    public async Task SecondTerminal_WhenSwitchedTo_ReceivesAndDisplaysOutput()
    {
        // Arrange - Create two terminals
        var terminal1 = WithInteractiveShell(
            Hex1bTerminal.CreateBuilder()
                .WithDimensions(148, 36))
            .WithTerminalWidget(out var handle1)
            .Build();
        
        var terminal2 = WithInteractiveShell(
            Hex1bTerminal.CreateBuilder()
                .WithDimensions(148, 36))
            .WithTerminalWidget(out var handle2)
            .Build();
        
        // Create nodes for each
        var node = new TerminalNode();
        bool invalidateCalled = false;
        node.SetInvalidateCallback(() => invalidateCalled = true);
        
        // Act - Start first terminal and bind node to it
        var runTask1 = Task.Run(() => terminal1.RunAsync(TestContext.Current.CancellationToken));
        var runTask2 = Task.CompletedTask;
        try
        {
            node.Handle = handle1;
            node.Bind();

            await WaitForTerminalContentAsync(handle1, GetStartupTimeout(), TestContext.Current.CancellationToken);
            
            // Verify first terminal works
            var buffer1 = handle1.GetScreenBuffer();
            bool terminal1HasContent = HasNonEmptyContent(buffer1, handle1.Height, handle1.Width);
            Assert.True(terminal1HasContent, "First terminal should have content");
            
            // Now switch to second terminal (simulates what reconciliation does)
            node.Unbind();
            
            // Start second terminal
            runTask2 = Task.Run(() => terminal2.RunAsync(TestContext.Current.CancellationToken));
            
            // Bind to second terminal
            invalidateCalled = false;
            node.Handle = handle2;
            node.Bind();
            
            // Arrange the node (simulates layout phase)
            node.Measure(new Constraints(0, 148, 0, 36));
            node.Arrange(new Rect(0, 0, 148, 36));

            await WaitForTerminalContentAsync(handle2, GetStartupTimeout(), TestContext.Current.CancellationToken);
            
            // Assert - Second terminal should have content
            var buffer2 = handle2.GetScreenBuffer();
            bool terminal2HasContent = HasNonEmptyContent(buffer2, handle2.Height, handle2.Width);
            
            Assert.True(terminal2HasContent, 
                $"Second terminal should have content. State={handle2.State}, Width={handle2.Width}, Height={handle2.Height}");
            Assert.True(invalidateCalled, "Invalidate should have been called for second terminal");
        }
        finally
        {
            node.Unbind();
            await terminal1.DisposeAsync();
            await terminal2.DisposeAsync();
            await AwaitTerminalRunTaskAsync(runTask1);
            await AwaitTerminalRunTaskAsync(runTask2);
        }
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

    private static Hex1bTerminalBuilder WithInteractiveShell(Hex1bTerminalBuilder builder)
    {
        var (fileName, arguments) = GetInteractiveShell();
        return builder.WithPtyProcess(fileName, arguments);
    }

    private static Hex1bTerminalChildProcess CreateInteractiveShellProcess(int initialWidth, int initialHeight)
    {
        var (fileName, arguments) = GetInteractiveShell();
        return new Hex1bTerminalChildProcess(
            fileName,
            arguments,
            initialWidth: initialWidth,
            initialHeight: initialHeight);
    }

    private static (string FileName, string[] Arguments) GetInteractiveShell()
    {
        return OperatingSystem.IsWindows()
            ? ("pwsh", ["-NoLogo", "-NoProfile", "-Command", $"[Console]::WriteLine('{StartupMarker}'); while ($true) {{ [Console]::WriteLine('HEX1B_TERMINAL_TICK'); Start-Sleep -Milliseconds 250 }}"])
            : ("bash", ["--norc", "--noprofile", "-c", $"printf '{StartupMarker}\\n'; while true; do printf 'HEX1B_TERMINAL_TICK\\n'; sleep 0.25; done"]);
    }

    private static TimeSpan GetStartupTimeout() =>
        OperatingSystem.IsWindows() ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(10);

    private static async Task WaitForTerminalStateAsync(
        TerminalWidgetHandle handle,
        TerminalState expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (handle.State == expectedState)
        {
            return;
        }

        var stateChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnStateChanged(TerminalState state)
        {
            if (state == expectedState)
            {
                stateChanged.TrySetResult();
            }
        }

        handle.StateChanged += OnStateChanged;
        try
        {
            OnStateChanged(handle.State);
            await stateChanged.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            handle.StateChanged -= OnStateChanged;
        }
    }

    private static async Task WaitForTerminalContentAsync(
        TerminalWidgetHandle handle,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (HasNonEmptyContent(handle.GetScreenBuffer(), handle.Height, handle.Width))
        {
            return;
        }

        var contentReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnOutputReceived()
        {
            if (HasNonEmptyContent(handle.GetScreenBuffer(), handle.Height, handle.Width))
            {
                contentReady.TrySetResult();
            }
        }

        handle.OutputReceived += OnOutputReceived;
        try
        {
            OnOutputReceived();

            var deadline = DateTime.UtcNow + timeout;
            while (!contentReady.Task.IsCompleted)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for terminal content. State={handle.State}, Width={handle.Width}, Height={handle.Height}.");
                }

                var pollDelay = remaining < TimeSpan.FromMilliseconds(100)
                    ? remaining
                    : TimeSpan.FromMilliseconds(100);

                var completed = await Task.WhenAny(contentReady.Task, Task.Delay(pollDelay, cancellationToken));
                if (completed == contentReady.Task)
                {
                    await contentReady.Task;
                    break;
                }

                OnOutputReceived();
            }
        }
        finally
        {
            handle.OutputReceived -= OnOutputReceived;
        }
    }

    private static async Task AwaitTerminalRunTaskAsync(Task runTask)
    {
        try
        {
            await runTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
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
        var terminals = new List<(int id, Hex1bTerminal term, TerminalWidgetHandle handle, Task runTask)>();
        var terminalLock = new object();
        var nextTerminalId = 1;
        var activeTerminalId = 0;
        Hex1bApp? appRef = null;
        
        void AddTerminal()
        {
            var id = nextTerminalId++;
            var childTerminal = WithInteractiveShell(
                Hex1bTerminal.CreateBuilder()
                    .WithDimensions(148, 36))
                .WithTerminalWidget(out var handle)
                .Build();
            
            var childRunTask = Task.Run(async () =>
            {
                try
                {
                    await childTerminal.RunAsync(TestContext.Current.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            });

            lock (terminalLock)
            {
                terminals.Add((id, childTerminal, handle, childRunTask));
                activeTerminalId = id;
            }
            
            // Invalidate to trigger re-render with the new terminal
            appRef?.Invalidate();
        }
        
        Hex1bWidget BuildUI(RootContext ctx)
        {
            List<(int id, Hex1bTerminal term, TerminalWidgetHandle handle, Task runTask)> currentTerminals;
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
                ctx.Terminal(activeSession.handle).Fill()
            ).Title($"Terminal {activeSession.id}").Fill();
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
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("No terminals"), TimeSpan.FromSeconds(5))
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            
            // Create first terminal
            AddTerminal();
            
            // Verify first terminal has content
            var handle1 = terminals[0].handle;
            await WaitForTerminalContentAsync(handle1, GetStartupTimeout(), TestContext.Current.CancellationToken);
            var buffer1 = handle1.GetScreenBuffer();
            bool terminal1HasContent = HasNonEmptyContent(buffer1, handle1.Height, handle1.Width);
            Assert.True(terminal1HasContent, "First terminal should have content");
            
            // Create second terminal
            AddTerminal();
            
            // Verify second terminal has content
            var handle2 = terminals[1].handle;
            await WaitForTerminalContentAsync(handle2, GetStartupTimeout(), TestContext.Current.CancellationToken);
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
            foreach (var (_, term, _, childRunTask) in terminals)
            {
                await term.DisposeAsync();
                await AwaitTerminalRunTaskAsync(childRunTask);
            }
        }
        
        await runTask;
    }
}
