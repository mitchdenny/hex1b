using Hex1b.Input;
using Hex1b.Widgets;
using Hex1bTheming = Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for render optimization - verifying that clean nodes are not re-rendered.
/// </summary>
public class RenderOptimizationTests
{
    [Fact]
    public async Task SameWidgetInstance_ShouldNotReRender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var renderCount = 0;

        // Create a single widget instance that's reused every frame
        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => testWidget,
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Trigger re-renders via input
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .Key(Hex1bKey.C)
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Same widget instance = same node = clean = only initial render
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task TextBlock_WithChangingText_ShouldReRender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var counter = 0;

        using var app = new Hex1bApp(
            ctx =>
            {
                counter++;
                // Text changes each frame - node should be marked dirty
                return Task.FromResult<Hex1bWidget>(ctx.Text($"Counter: {counter}"));
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .WaitUntil(s => s.ContainsText("Counter: 3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Each frame has different text, so each frame should render
        // Initial (Counter: 1) + 'a' (Counter: 2) + 'b' (Counter: 3) = 3 renders before Ctrl+C
        // Verify by checking the final output before Ctrl+C
        Assert.True(snapshot.ContainsText("Counter: 3"));
    }

    [Fact]
    public async Task TextBlock_WithSameText_ShouldNotReRender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var renderCount = 0;
        
        // Track renders by wrapping in a VStack with a TestWidget
        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Static text"), // Same text every frame
                testWidget // Track renders - should only render once
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Both widgets have same content each frame - only initial render
        Assert.Equal(1, renderCount);
    }

    [Fact]
    public async Task MixedTree_StaticAndDynamic_AllNodesRenderEveryFrame()
    {
        // In Surface mode, all nodes render every frame to the surface.
        // Optimization happens at the Surface diffing level, not at the node level.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
                        staticWidget,
                        v.Text($"Counter: {counter}")
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .WaitUntil(s => s.ContainsText("Counter: 3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // In Surface mode, static widgets render every frame (optimization is at diff level)
        // Counter goes 1, 2, 3 = 3 frames plus possible initial frame
        Assert.True(staticRenderCount >= 3, $"Expected at least 3 renders, got {staticRenderCount}");
        
        // Dynamic text verified by WaitUntil above - "Counter: 3" was confirmed before Ctrl+C
    }

    [Fact]
    public async Task VStack_IndexBasedReconciliation_CreatesNewNodeWhenWidgetMoves()
    {
        // Verifies that VStack's index-based reconciliation correctly creates new nodes
        // when a widget's position changes (rather than reusing the old node)
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)
            .Key(Hex1bKey.B)
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Frame 1: testWidget at index 0 → new node
        // Frame 2: testWidget at index 0 → same node reused  
        // Frame 3: testWidget at index 1 → new node (because VStack uses index-based reconciliation)
        // Frame 4: Ctrl+C triggers another frame (testWidget still at index 1, reused)
        Assert.Equal(4, reconcileDetails.Count);
        Assert.True(reconcileDetails[0].isNewNode);  // Frame 1: new
        Assert.False(reconcileDetails[1].isNewNode); // Frame 2: reused
        Assert.True(reconcileDetails[2].isNewNode);  // Frame 3: new (widget moved to index 1)
        Assert.False(reconcileDetails[3].isNewNode); // Frame 4: reused
    }

    [Fact]
    public async Task SingleFrame_RendersNewNode()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // First render only
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // After first frame and Ctrl+C frame, verify node state
        Assert.NotNull(capturedNode);
        Assert.Equal(2, reconcileCount); // Initial + Ctrl+C frame
        Assert.Equal(1, renderCount); // Node is clean on second frame, so only 1 render
    }

    [Fact]
    public async Task NodeWithChangedBounds_ShouldReRender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableInputCoalescing = false }
        );

        // Frame 1: [testWidget] - new node, rendered
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.A)  // triggers frame 2: [testWidget] - same node, clean, NOT rendered
            .Key(Hex1bKey.B)  // triggers frame 3: [Text, testWidget] - new node at index 1, rendered
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

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
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var renderCount = 0;

        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [testWidget]),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableMouse = true
            }
        );

        // Move mouse multiple times - with native cursor, this should NOT trigger re-renders
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(5, 5)
            .MouseMoveTo(10, 10)
            .MouseMoveTo(15, 15)
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

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
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var rightPaneRenderCount = 0;

        using var app = new Hex1bApp(
            ctx => ctx.HSplitter(
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
                WorkloadAdapter = workload,
                EnableMouse = true
            }
        );

        // Initial render
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)  // Let the app initialize
            // Start drag on the splitter divider (at x=21 which is inside the " │ " divider at 20-22)
            // Drag right by 5 characters
            .Drag(21, 5, 26, 5)
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The widget in the right pane should re-render when the splitter moves
        // because its bounds change: initial render (1) + after drag moves bounds (2)
        Assert.True(rightPaneRenderCount >= 2, $"Expected at least 2 renders but got {rightPaneRenderCount}. Splitter drag should trigger re-render.");
    }

    [Fact]
    public async Task SplitterDrag_DividerShouldReRenderAtNewPosition()
    {
        // The splitter divider itself should re-render at its new position when dragged.
        // This test verifies the splitter node is marked dirty when FirstSize changes.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => ctx.HSplitter(
                left => [left.Text("Left")],
                right => [right.Text("Right")],
                leftWidth: 20
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableMouse = true
            }
        );

        // Start drag on the splitter divider (at x=21 which is inside the " │ " divider at 20-22)
        // Drag right by 10 characters
        var dividerMoved = false;
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left") && s.ContainsText("Right"), TimeSpan.FromSeconds(2), "splitter to render")
            .Drag(21, 5, 31, 5)
            .WaitUntil(s =>
            {
                // Check if the divider moved - look for │ around position 31
                var output = s.GetScreenText();
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Length > 31 && line[31] == '│')
                    {
                        dividerMoved = true;
                        return true;
                    }
                }
                return false;
            }, TimeSpan.FromSeconds(2), "divider to move to new position")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(dividerMoved, "Divider should have moved to new position after drag");
    }

    [Fact]
    public async Task TerminalResize_Enlarge_ShouldNotCrash()
    {
        // Verifies that enlarging the terminal doesn't cause an index out of bounds exception
        // This was a bug where _width/_height were updated before Resize() was called,
        // causing the copy loop to try accessing indices beyond the old buffer.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        
        // Enlarge the terminal - this should not crash
        terminal.Resize(120, 40);
        
        // Verify new dimensions
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal(120, snapshot.Width);
        Assert.Equal(40, snapshot.Height);
    }

    [Fact]
    public async Task TerminalResize_ShouldTriggerFullReRender()
    {
        // When the terminal is resized, all nodes should re-render
        // because their layout may have changed.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var renderCount = 0;

        var testWidget = new TestWidget().OnRender(_ => renderCount++);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [testWidget]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Trigger a resize event
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await workload.ResizeAsync(100, 30, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should render twice: initial render + after resize
        Assert.True(renderCount >= 2, $"Expected at least 2 renders but got {renderCount}. Resize should trigger full re-render.");
    }

    [Fact]
    public async Task ButtonHoverInPanel_ShouldPreserveBackgroundColor()
    {
        // This test verifies that when a button inside a panel becomes hovered,
        // the dirty region clearing should use the panel's background color,
        // not the terminal's default background.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        
        // Use a theme with a visible panel background color
        var panelBgColor = Hex1bTheming.Hex1bColor.FromRgb(30, 30, 60); // Dark blue
        var theme = new Hex1bTheming.Hex1bTheme("Test")
            .Set(Hex1bTheming.GlobalTheme.BackgroundColor, panelBgColor);

        using var app = new Hex1bApp(
            ctx => ctx.ThemePanel(
                t => t.Set(Hex1bTheming.GlobalTheme.BackgroundColor, panelBgColor),
                ctx.VStack(v => [
                    v.Text("Label before button"),
                    v.Button("Click Me"),
                    v.Text("Label after button")
                ])
            ),
            new Hex1bAppOptions 
            { 
                WorkloadAdapter = workload,
                EnableMouse = true,
                Theme = theme
            }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Wait for initial render, then move mouse over button to trigger hover state
        var finalSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .MouseMoveTo(5, 1)
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Find any cells that have null (default) background instead of the panel color
        // We specifically check the area to the right of the button on the same row
        // The button is "[ Click Me ]" which is about 13 characters, starting around column 0
        // Check columns 15-40 on row 1 - these should still have the panel background
        var mismatches = finalSnapshot.FindMismatchedBackgrounds(15, 1, 25, 1, panelBgColor);
        
        Assert.True(mismatches.Count == 0,
            $"Expected cells to the right of button to have panel background color {panelBgColor}, " +
            $"but found {mismatches.Count} cells with wrong background.\n" +
            $"Background visualization:\n{finalSnapshot.VisualizeBackgroundColors(0, 0, 40, 5, panelBgColor)}\n" +
            $"Mismatched cells: {string.Join(", ", mismatches.Select(m => $"({m.X},{m.Y})={m.ActualBackground?.ToString() ?? "null"}"))}");
    }
}
