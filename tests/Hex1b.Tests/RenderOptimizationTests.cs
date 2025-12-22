using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for render optimization - verifying that clean nodes are not re-rendered.
/// </summary>
public class RenderOptimizationTests
{
    [Fact]
    public async Task SameWidgetInstance_ShouldNotReRender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;

        // Create a single widget instance that's reused every frame
        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => testWidget,
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        // Trigger re-renders via input
        terminal.SendKey(ConsoleKey.A, 'a');
        terminal.SendKey(ConsoleKey.B, 'b');
        terminal.SendKey(ConsoleKey.C, 'c');
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // Same widget instance = same node = clean = only initial render
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task TextBlock_WithChangingText_ShouldReRender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var counter = 0;

        using var app = new Hex1bApp(
            ctx =>
            {
                counter++;
                // Text changes each frame - node should be marked dirty
                return Task.FromResult<Hex1bWidget>(ctx.Text($"Counter: {counter}"));
            },
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        terminal.SendKey(ConsoleKey.A, 'a');
        terminal.SendKey(ConsoleKey.B, 'b');
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // Each frame has different text, so each frame should render
        // Initial (Counter: 1) + 'a' (Counter: 2) + 'b' (Counter: 3) = 3 renders
        // Verify by checking the final output
        Assert.True(terminal.ContainsText("Counter: 3"));
    }

    [Fact]
    public async Task TextBlock_WithSameText_ShouldNotReRender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;
        
        // Track renders by wrapping in a VStack with a TestWidget
        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Static text"), // Same text every frame
                testWidget // Track renders - should only render once
            ]),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        terminal.SendKey(ConsoleKey.A, 'a');
        terminal.SendKey(ConsoleKey.B, 'b');
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // Both widgets have same content each frame - only initial render
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task MixedTree_StaticAndDynamic_OnlyDynamicReRenders()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var staticRenderCount = 0;
        var counter = 0;

        // Static widget - same instance every frame
        var staticWidget = new TestWidget().OnRender(_ => staticRenderCount++);

        using var app = new Hex1bApp(
            ctx =>
            {
                counter++;
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [
                        // Static widget - should only render once
                        staticWidget,
                        // Dynamic text - changes every frame
                        v.Text($"Counter: {counter}")
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        terminal.SendKey(ConsoleKey.A, 'a');
        terminal.SendKey(ConsoleKey.B, 'b');
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // The static widget should only render once (initial frame)
        Assert.Equal(1, staticRenderCount);
        
        // Dynamic text should have updated
        Assert.True(terminal.ContainsText("Counter: 3"));
    }

    [Fact]
    public async Task VStack_IndexBasedReconciliation_CreatesNewNodeWhenWidgetMoves()
    {
        // Verifies that VStack's index-based reconciliation correctly creates new nodes
        // when a widget's position changes (rather than reusing the old node)
        using var terminal = new Hex1bTerminal(80, 24);
        var reconcileDetails = new List<(int frame, bool isNewNode)>();
        var frameNumber = 0;

        var testWidget = new TestWidget()
            .OnReconcile(e => 
            {
                reconcileDetails.Add((frameNumber, e.ExistingNode == null));
            });

        using var app = new Hex1bApp(
            ctx => 
            {
                frameNumber++;
                // Show extra starting from frame 3
                var showExtra = frameNumber >= 3;
                if (showExtra)
                {
                    return Task.FromResult<Hex1bWidget>(
                        ctx.VStack(v => [v.Text("Extra line"), testWidget])
                    );
                }
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [testWidget])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        terminal.SendKey(ConsoleKey.A, 'a');
        terminal.SendKey(ConsoleKey.B, 'b');
        terminal.CompleteInput();

        await app.RunAsync();

        // Frame 1: testWidget at index 0 → new node
        // Frame 2: testWidget at index 0 → same node reused  
        // Frame 3: testWidget at index 1 → new node (because VStack uses index-based reconciliation)
        Assert.Equal(3, reconcileDetails.Count);
        Assert.True(reconcileDetails[0].isNewNode);  // Frame 1: new
        Assert.False(reconcileDetails[1].isNewNode); // Frame 2: reused
        Assert.True(reconcileDetails[2].isNewNode);  // Frame 3: new (widget moved to index 1)
    }

    [Fact]
    public async Task SingleFrame_RendersNewNode()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;
        var reconcileCount = 0;
        Hex1bNode? capturedNode = null;

        var testWidget = new TestWidget()
            .OnRender(_ => renderCount++)
            .OnReconcile(e => 
            {
                reconcileCount++;
                capturedNode = e.Node;
            });

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [testWidget])
            ),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        // First render only
        terminal.CompleteInput();
        await app.RunAsync();

        // After first frame, verify node state
        Assert.NotNull(capturedNode);
        Assert.Equal(1, reconcileCount);
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task NodeWithChangedBounds_ShouldReRender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;
        var frameNumber = 0;

        var testWidget = new TestWidget()
            .OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => 
            {
                frameNumber++;
                // Show extra starting from frame 3
                var showExtra = frameNumber >= 3;
                if (showExtra)
                {
                    return Task.FromResult<Hex1bWidget>(
                        ctx.VStack(v => [v.Text("Extra line"), testWidget])
                    );
                }
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [testWidget])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        // Frame 1: [testWidget] - new node, rendered
        terminal.SendKey(ConsoleKey.A, 'a'); // triggers frame 2: [testWidget] - same node, clean, NOT rendered
        terminal.SendKey(ConsoleKey.B, 'b'); // triggers frame 3: [Text, testWidget] - new node at index 1, rendered
        terminal.CompleteInput();

        await app.RunAsync();

        // Due to VStack's index-based reconciliation:
        // - Frame 1: testWidget at index 0 → new TestWidgetNode created → rendered (1)
        // - Frame 2 (after 'a'): testWidget at index 0 → same node reused → clean → NOT rendered (1)  
        // - Frame 3 (after 'b'): testWidget at index 1 → NEW TestWidgetNode created → rendered (2)
        Assert.Equal(2, renderCount);
    }

    [Fact]
    public async Task MouseCursorMove_WithNativeCursor_ShouldNotCauseExtraRenders()
    {
        // This test verifies that mouse cursor movement using the terminal's native cursor
        // does not cause extra re-renders. The native cursor is rendered by the terminal
        // itself, so we don't need to mark widgets dirty when the cursor moves.
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;

        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [testWidget]),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = terminal.WorkloadAdapter,
                EnableMouse = true
            }
        );

        // Move mouse multiple times - with native cursor, this should NOT trigger re-renders
        terminal.SendMouse(MouseButton.None, MouseAction.Move, 5, 5);
        terminal.SendMouse(MouseButton.None, MouseAction.Move, 10, 10);
        terminal.SendMouse(MouseButton.None, MouseAction.Move, 15, 15);
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // With native terminal cursor, mouse movement should NOT trigger extra renders.
        // The widget should only render once (initial frame) since we use the terminal's
        // native cursor instead of drawing a colored block that overwrites content.
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task SplitterDrag_ShouldReRenderSplitterAndChildren()
    {
        // When dragging the splitter, FirstSize changes and the splitter should be marked dirty
        // so that the divider and children re-render at their new positions.
        using var terminal = new Hex1bTerminal(80, 24);
        var rightPaneRenderCount = 0;

        using var app = new Hex1bApp(
            ctx => ctx.Splitter(
                left => [
                    left.Text("Left pane")
                ],
                right => [
                    new TestWidget().OnRender(_ => rightPaneRenderCount++)
                ],
                leftWidth: 20
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = terminal.WorkloadAdapter,
                EnableMouse = true
            }
        );

        // Initial render
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Let the app initialize
        
        // Start drag on the splitter divider (at x=21 which is inside the " │ " divider at 20-22)
        terminal.SendMouse(MouseButton.Left, MouseAction.Down, 21, 5);
        // Drag right by 5 characters
        terminal.SendMouse(MouseButton.Left, MouseAction.Drag, 26, 5);
        // Release
        terminal.SendMouse(MouseButton.Left, MouseAction.Up, 26, 5);
        
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // The widget in the right pane should re-render when the splitter moves
        // because its bounds change: initial render (1) + after drag moves bounds (2)
        Assert.True(rightPaneRenderCount >= 2, $"Expected at least 2 renders but got {rightPaneRenderCount}. Splitter drag should trigger re-render.");
    }

    [Fact]
    public async Task SplitterDrag_DividerShouldReRenderAtNewPosition()
    {
        // The splitter divider itself should re-render at its new position when dragged.
        // This test verifies the splitter node is marked dirty when FirstSize changes.
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp(
            ctx => ctx.Splitter(
                left => [left.Text("Left")],
                right => [right.Text("Right")],
                leftWidth: 20
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = terminal.WorkloadAdapter,
                EnableMouse = true
            }
        );

        // Start drag on the splitter divider (at x=21 which is inside the " │ " divider at 20-22)
        terminal.SendMouse(MouseButton.Left, MouseAction.Down, 21, 5);
        // Drag right by 10 characters 
        terminal.SendMouse(MouseButton.Left, MouseAction.Drag, 31, 5);
        // Release
        terminal.SendMouse(MouseButton.Left, MouseAction.Up, 31, 5);
        
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();
        var output = terminal.GetScreenText();

        // After dragging, the divider should be at the new position (around column 30)
        // Check that the divider character "│" appears at the expected position
        // The divider is at FirstSize + 1 = 30 + 1 = 31
        // Split output into lines and check one of them
        var lines = output.Split('\n');
        var foundDividerAtNewPosition = false;
        foreach (var line in lines)
        {
            // The divider should be around position 30-32 (after moving from 20-22)
            if (line.Length > 31)
            {
                var charAtPos = line[31];
                if (charAtPos == '│')
                {
                    foundDividerAtNewPosition = true;
                    break;
                }
            }
        }

        Assert.True(foundDividerAtNewPosition, 
            $"Expected divider '│' at position 31 after drag. Output:\n{output}");
    }

    [Fact]
    public void TerminalResize_Enlarge_ShouldNotCrash()
    {
        // Verifies that enlarging the terminal doesn't cause an index out of bounds exception
        // This was a bug where _width/_height were updated before Resize() was called,
        // causing the copy loop to try accessing indices beyond the old buffer.
        using var terminal = new Hex1bTerminal(80, 24);
        
        // Write some content first
        terminal.FlushOutput();
        
        // Enlarge the terminal - this should not crash
        terminal.Resize(120, 40);
        
        // Verify new dimensions
        Assert.Equal(120, terminal.Width);
        Assert.Equal(40, terminal.Height);
    }

    [Fact]
    public async Task TerminalResize_ShouldTriggerFullReRender()
    {
        // When the terminal is resized, all nodes should re-render
        // because their layout may have changed.
        using var terminal = new Hex1bTerminal(80, 24);
        var renderCount = 0;

        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [testWidget]),
            new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
        );

        // Trigger a resize event
        await terminal.WorkloadAdapter.ResizeAsync(100, 30);
        
        terminal.CompleteInput();

        await app.RunAsync();
        terminal.FlushOutput();

        // Should render twice: initial render + after resize
        Assert.True(renderCount >= 2, $"Expected at least 2 renders but got {renderCount}. Resize should trigger full re-render.");
    }
}
