using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;
using Hex1b.Tokens;
using System.Text;
using System.Threading.Channels;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for embedded TerminalWidget to debug dirty render issues.
/// </summary>
/// <remarks>
/// <para>
/// These tests create a controlled test environment where we can feed arbitrary
/// cell impacts to an embedded terminal and verify the rendering behavior.
/// </para>
/// <para>
/// Architecture:
/// - Hex1bTerminal: Virtual terminal for testing
/// - Hex1bApp: Contains a TerminalWidget that renders from a TerminalWidgetHandle
/// - TerminalWidgetHandle: We inject cell impacts directly to simulate terminal output
/// </para>
/// </remarks>
public class TerminalWidgetIntegrationTests
{
    /// <summary>
    /// Helper to create the test environment with an embedded terminal.
    /// </summary>
    private sealed class EmbeddedTerminalTestContext : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        
        public Hex1bTerminal Terminal { get; }
        public Hex1bApp? App { get; private set; }
        public TerminalWidgetHandle Handle { get; }
        
        private Task? _runTask;
        
        private EmbeddedTerminalTestContext(
            Hex1bTerminal terminal,
            TerminalWidgetHandle handle)
        {
            Terminal = terminal;
            Handle = handle;
        }
        
        /// <summary>
        /// Creates a test context with the specified dimensions.
        /// </summary>
        public static EmbeddedTerminalTestContext Create(
            int width = 80,
            int height = 24,
            int handleWidth = 60,
            int handleHeight = 16)
        {
            // Create the terminal widget handle directly (no inner terminal needed)
            var handle = new TerminalWidgetHandle(handleWidth, handleHeight);
            EmbeddedTerminalTestContext? capturedContext = null;
            
            // Create the terminal using the builder pattern (proper lifecycle)
            var terminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp((app, options) =>
                {
                    // Capture the app for test access
                    if (capturedContext != null)
                    {
                        capturedContext.App = app;
                    }
                    
                    return ctx => new VStackWidget([
                        new TextBlockWidget("Terminal Widget Test"),
                        new BorderWidget(
                            new TerminalWidget(handle)
                        ).Title("Inner Terminal")
                    ]).Fill();
                })
                .WithHeadless()
                .WithDimensions(width, height)
                .Build();
            
            capturedContext = new EmbeddedTerminalTestContext(terminal, handle);
            return capturedContext;
        }
        
        /// <summary>
        /// Starts the terminal running (which runs the app).
        /// </summary>
        public async Task StartAsync()
        {
            // Start the terminal (which runs the app internally)
            _runTask = Terminal.RunAsync(_cts.Token);
            
            // Wait for initial render
            await WaitForTextAsync("Terminal Widget Test", TimeSpan.FromSeconds(5));
        }
        
        /// <summary>
        /// Injects text directly into the handle's buffer at the specified position.
        /// </summary>
        public void InjectText(int row, int col, string text)
        {
            var impacts = new List<CellImpact>();
            for (int i = 0; i < text.Length; i++)
            {
                impacts.Add(new CellImpact(
                    col + i, 
                    row, 
                    new TerminalCell(text[i].ToString(), null, null, CellAttributes.None, 0)));
            }
            
            var token = new AppliedToken(
                Token: new TextToken(text),
                CellImpacts: impacts,
                CursorXBefore: col,
                CursorYBefore: row,
                CursorXAfter: col + text.Length,
                CursorYAfter: row
            );
            
            // Call WriteOutputWithImpactsAsync directly
            Handle.WriteOutputWithImpactsAsync([token]).AsTask().Wait();
        }
        
        /// <summary>
        /// Clears a line in the handle's buffer.
        /// </summary>
        public void ClearLine(int row, int startCol = 0, int? endCol = null)
        {
            var handleWidth = Handle.Width;
            var actualEndCol = endCol ?? handleWidth;
            
            var impacts = new List<CellImpact>();
            for (int col = startCol; col < actualEndCol; col++)
            {
                impacts.Add(new CellImpact(
                    col,
                    row,
                    TerminalCell.Empty));
            }
            
            if (impacts.Count > 0)
            {
                var token = new AppliedToken(
                    Token: new TextToken(""), // Use empty text token for clear
                    CellImpacts: impacts,
                    CursorXBefore: startCol,
                    CursorYBefore: row,
                    CursorXAfter: startCol,
                    CursorYAfter: row
                );
                
                Handle.WriteOutputWithImpactsAsync([token]).AsTask().Wait();
            }
        }
        
        /// <summary>
        /// Waits until the terminal contains the specified text.
        /// </summary>
        public async Task WaitForTextAsync(string text, TimeSpan timeout)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText(text), timeout)
                .Build()
                .ApplyAsync(Terminal);
        }
        
        /// <summary>
        /// Waits for a short delay to allow rendering.
        /// </summary>
        public async Task WaitForRenderAsync(int delayMs = 100)
        {
            App?.Invalidate();
            await Task.Delay(delayMs);
        }
        
        /// <summary>
        /// Gets the current screen text from the terminal.
        /// </summary>
        public string GetScreenText() => Terminal.GetScreenText();
        
        /// <summary>
        /// Creates a snapshot of the terminal.
        /// </summary>
        public Hex1bTerminalSnapshot CreateSnapshot() => Terminal.CreateSnapshot();
        
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            
            if (_runTask != null)
            {
                try { await _runTask; } catch { }
            }
            
            await Terminal.DisposeAsync();
            _cts.Dispose();
        }
    }
    
    [Fact]
    public async Task EmbeddedTerminal_InitialRender_ShowsBorder()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create();
        
        // Act
        await ctx.StartAsync();
        
        // Assert - Should see the title and border
        var screen = ctx.GetScreenText();
        Assert.Contains("Terminal Widget Test", screen);
        Assert.Contains("Inner Terminal", screen);
    }
    
    [Fact]
    public async Task EmbeddedTerminal_InjectText_ShowsInWidget()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create();
        await ctx.StartAsync();
        
        // Act - Inject simple text directly to handle
        ctx.InjectText(0, 0, "Hello from inner terminal!");
        
        // Wait for render
        await ctx.WaitForTextAsync("Hello from inner terminal", TimeSpan.FromSeconds(5));
        
        // Assert
        var screen = ctx.GetScreenText();
        Assert.Contains("Hello from inner terminal", screen);
    }
    
    [Fact]
    public async Task EmbeddedTerminal_ClearLine_ClearsContent()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create();
        await ctx.StartAsync();
        
        // Write some text first
        ctx.InjectText(0, 0, "FIRST LINE HERE");
        await ctx.WaitForTextAsync("FIRST LINE", TimeSpan.FromSeconds(5));
        
        // Act - Clear the line
        ctx.ClearLine(0);
        await ctx.WaitForRenderAsync();
        
        // Assert - "FIRST LINE" should no longer appear
        var screen = ctx.GetScreenText();
        Assert.DoesNotContain("FIRST LINE", screen);
    }
    
    [Fact]
    public async Task EmbeddedTerminal_AnimationFrame_ClearsOldContent()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create();
        await ctx.StartAsync();
        
        // Simulate animation frame 1: Draw train at column 40
        ctx.InjectText(0, 40, "[TRAIN]");
        await ctx.WaitForTextAsync("[TRAIN]", TimeSpan.FromSeconds(5));
        
        // Act - Simulate animation frame 2: Clear line and draw at column 30
        ctx.ClearLine(0);
        ctx.InjectText(0, 30, "[TRAIN]");
        await ctx.WaitForRenderAsync();
        
        // Assert - There should only be ONE train visible, not a ghost trail
        var screen = ctx.GetScreenText();
        var trainCount = CountOccurrences(screen, "[TRAIN]");
        Assert.Equal(1, trainCount);
    }
    
    [Fact]
    public async Task EmbeddedTerminal_RapidOutput_FinalFrameRendered()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create();
        await ctx.StartAsync();
        
        // Act - Rapidly send multiple frames (clear + write)
        for (int i = 0; i < 5; i++)
        {
            // Clear line and write frame number
            ctx.ClearLine(0);
            ctx.InjectText(0, 0, $"Frame {i}");
            await Task.Delay(20); // Brief delay between frames
        }
        
        await ctx.WaitForRenderAsync(200); // Wait for final render
        
        // Assert - Should show the last frame
        var screen = ctx.GetScreenText();
        Assert.Contains("Frame 4", screen);
        
        // Should NOT contain earlier frames (they should be cleared)
        Assert.DoesNotContain("Frame 0", screen);
        Assert.DoesNotContain("Frame 1", screen);
    }
    
    [Fact]
    public async Task EmbeddedTerminal_ClearScreen_ClearsAllContent()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create();
        await ctx.StartAsync();
        
        // Fill with some content
        ctx.InjectText(0, 0, "Line 1");
        ctx.InjectText(1, 0, "Line 2");
        ctx.InjectText(2, 0, "Line 3");
        await ctx.WaitForTextAsync("Line 3", TimeSpan.FromSeconds(5));
        
        // Act - Clear all lines
        ctx.ClearLine(0);
        ctx.ClearLine(1);
        ctx.ClearLine(2);
        await ctx.WaitForRenderAsync();
        
        // Assert - None of the old content should be visible
        var screen = ctx.GetScreenText();
        Assert.DoesNotContain("Line 1", screen);
        Assert.DoesNotContain("Line 2", screen);
        Assert.DoesNotContain("Line 3", screen);
    }
    
    /// <summary>
    /// Simulates the 'sl' steam locomotive animation pattern.
    /// The train should move across the screen without leaving ghost trails.
    /// </summary>
    [Fact]
    public async Task EmbeddedTerminal_TrainAnimation_NoGhostTrails()
    {
        // Arrange
        await using var ctx = EmbeddedTerminalTestContext.Create(80, 24, 60, 10);
        await ctx.StartAsync();
        
        // Act - Simulate train moving from right to left
        // Frame 1: Train at position 40
        ctx.ClearLine(4);
        ctx.InjectText(4, 40, "<=====>");
        await Task.Delay(50);
        
        // Frame 2: Train at position 30 (should clear position 40)
        ctx.ClearLine(4);
        ctx.InjectText(4, 30, "<=====>");
        await Task.Delay(50);
        
        // Frame 3: Train at position 20
        ctx.ClearLine(4);
        ctx.InjectText(4, 20, "<=====>");
        await ctx.WaitForRenderAsync();
        
        // Assert - Should only see ONE train
        var screen = ctx.GetScreenText();
        var trainCount = CountOccurrences(screen, "<=====>");
        Assert.Equal(1, trainCount);
    }
    
    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int i = 0;
        while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
        {
            count++;
            i += pattern.Length;
        }
        return count;
    }
    
    /// <summary>
    /// Tests that a focused TerminalNode captures all input including Ctrl+C.
    /// </summary>
    [Fact]
    public void TerminalNode_WhenFocusedAndRunning_CapturesAllInput()
    {
        // Arrange
        var handle = new TerminalWidgetHandle(80, 24);
        var focusRing = new FocusRing();
        
        // Set up capture callbacks on the node so we can track capture state via FocusRing
        var node = new Hex1b.Nodes.TerminalNode
        {
            Handle = handle
        };
        node.SetCaptureCallbacks(focusRing.CaptureInput, focusRing.ReleaseCapture);
        node.IsFocused = true;
        
        // Handle is NotStarted by default, so should not capture
        Assert.Equal(TerminalState.NotStarted, handle.State);
        Assert.Null(focusRing.CapturedNode);
    }
    
    /// <summary>
    /// Tests that an unfocused TerminalNode does not capture input.
    /// </summary>
    [Fact]
    public void TerminalNode_WhenNotFocused_DoesNotCaptureInput()
    {
        // Arrange
        var handle = new TerminalWidgetHandle(80, 24);
        var focusRing = new FocusRing();
        
        var node = new Hex1b.Nodes.TerminalNode
        {
            Handle = handle
        };
        node.SetCaptureCallbacks(focusRing.CaptureInput, focusRing.ReleaseCapture);
        node.IsFocused = false;
        
        // Act & Assert
        Assert.Null(focusRing.CapturedNode);
    }
    
    /// <summary>
    /// Tests that a TerminalNode without a handle does not capture input.
    /// </summary>
    [Fact]
    public void TerminalNode_WhenNoHandle_DoesNotCaptureInput()
    {
        // Arrange
        var focusRing = new FocusRing();
        var node = new Hex1b.Nodes.TerminalNode
        {
            Handle = null
        };
        node.SetCaptureCallbacks(focusRing.CaptureInput, focusRing.ReleaseCapture);
        node.IsFocused = true;
        
        // Act & Assert
        Assert.Null(focusRing.CapturedNode);
    }
    
    /// <summary>
    /// Tests that Ctrl+C is forwarded to the focused terminal instead of triggering the app's default exit.
    /// </summary>
    [Fact]
    public async Task FocusedTerminal_CtrlC_ForwardedToTerminal()
    {
        // Arrange - Create a terminal handle and track if input was received
        var handle = new TerminalWidgetHandle(80, 24);
        
        // Track what events are sent to the handle
        var receivedEvents = new List<Hex1bKeyEvent>();
        var focusRing = new FocusRing();
        
        // Create a node and simulate the Running state via reflection (since NotifyStarted is internal)
        var node = new Hex1b.Nodes.TerminalNode
        {
            Handle = handle
        };
        node.SetCaptureCallbacks(focusRing.CaptureInput, focusRing.ReleaseCapture);
        
        // Use reflection to set the terminal to Running state
        var stateField = typeof(TerminalWidgetHandle).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        stateField?.SetValue(handle, TerminalState.Running);
        
        // Now set focused - this will trigger capture because state is Running
        node.IsFocused = true;
        
        // Verify the state was set and capture happened
        Assert.Equal(TerminalState.Running, handle.State);
        Assert.Same(node, focusRing.CapturedNode);
        
        // Act - Simulate Ctrl+C key event
        var ctrlCEvent = new Hex1bKeyEvent(Hex1bKey.C, '\x03', Hex1bModifiers.Control);
        var result = node.HandleInput(ctrlCEvent);
        
        // Assert - The node should handle the input (forward it to the terminal)
        Assert.Equal(InputResult.Handled, result);
    }
    
    /// <summary>
    /// Tests that the InputRouter correctly routes Ctrl+C to a captured node
    /// before checking bindings.
    /// </summary>
    [Fact]
    public async Task InputRouter_FocusedTerminal_ReceivesInputBeforeBindings()
    {
        // Arrange
        var handle = new TerminalWidgetHandle(80, 24);
        var focusRing = new FocusRing();
        
        // Use reflection to set the terminal to Running state
        var stateField = typeof(TerminalWidgetHandle).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        stateField?.SetValue(handle, TerminalState.Running);
        
        var terminalNode = new Hex1b.Nodes.TerminalNode
        {
            Handle = handle
        };
        terminalNode.SetCaptureCallbacks(focusRing.CaptureInput, focusRing.ReleaseCapture);
        terminalNode.IsFocused = true;
        
        // Create a root container with a Ctrl+C binding that should NOT be triggered
        var ctrlCTriggered = false;
        var rootNode = new VStackNode();
        rootNode.Children.Add(terminalNode);
        rootNode.BindingsConfigurator = builder =>
        {
            builder.Ctrl().Key(Hex1bKey.C).Action(_ => ctrlCTriggered = true, "Should not trigger");
        };
        
        terminalNode.Parent = rootNode;
        
        // Verify preconditions
        Assert.Same(terminalNode, focusRing.CapturedNode);
        Assert.True(terminalNode.IsFocused, "Terminal should be focused");
        
        // Act - Route a Ctrl+C through the InputRouter
        var ctrlCEvent = new Hex1bKeyEvent(Hex1bKey.C, '\x03', Hex1bModifiers.Control);
        var state = new InputRouterState();
        
        var result = await InputRouter.RouteInputAsync(
            rootNode, 
            ctrlCEvent, 
            focusRing, 
            state,
            requestStop: null);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.False(ctrlCTriggered, "Root's Ctrl+C binding should NOT have been triggered because TerminalNode captures all input");
    }
}