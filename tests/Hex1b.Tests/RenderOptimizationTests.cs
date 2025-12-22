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
}
