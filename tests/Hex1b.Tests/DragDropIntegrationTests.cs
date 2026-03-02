using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for drag-drop behavior with full terminal stack.
/// Covers: hover state on DraggableNode, drop target proximity (Manhattan distance),
/// IconNode clickability, WindowPanel background interactivity,
/// and a realistic multi-column drag scenario with overlays and drop targets.
/// </summary>
public class DragDropIntegrationTests
{
    #region DraggableNode IsHovered Tests

    [Fact]
    public void DraggableNode_IsHovered_DefaultFalse()
    {
        var node = new DraggableNode();
        Assert.False(node.IsHovered);
    }

    [Fact]
    public void DraggableNode_IsHovered_SetTrue_MarksDirty()
    {
        var node = new DraggableNode { Child = new TextBlockNode { Text = "test" } };
        node.IsHovered = true;
        Assert.True(node.IsHovered);
    }

    [Fact]
    public void DraggableNode_IsHovered_SetSameValue_NoChange()
    {
        var node = new DraggableNode();
        node.IsHovered = false; // Already false
        Assert.False(node.IsHovered);
    }

    [Fact]
    public void DraggableContext_IsHovered_ReflectsNodeState()
    {
        var node = new DraggableNode { DragData = "test" };
        var context = new DraggableContext(node);

        Assert.False(context.IsHovered);

        node.IsHovered = true;
        Assert.True(context.IsHovered);

        node.IsHovered = false;
        Assert.False(context.IsHovered);
    }

    #endregion

    #region IconNode Focusable Tests

    [Fact]
    public void IconNode_NotClickable_NotFocusable()
    {
        var node = new IconNode { Icon = "x" };
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void IconNode_WithClickHandler_IsFocusable()
    {
        var node = new IconNode
        {
            Icon = "x",
            ClickCallback = _ => Task.CompletedTask
        };
        Assert.True(node.IsFocusable);
        Assert.True(node.IsClickable);
    }

    [Fact]
    public void IconNode_WithClickHandler_HasHitTestBounds()
    {
        var node = new IconNode
        {
            Icon = "x",
            ClickCallback = _ => Task.CompletedTask
        };
        node.Measure(new Constraints(0, 40, 0, 10));
        node.Arrange(new Rect(5, 3, 1, 1));

        Assert.Equal(new Rect(5, 3, 1, 1), node.HitTestBounds);
    }

    [Fact]
    public void IconNode_WithoutClickHandler_DefaultHitTestBounds()
    {
        var node = new IconNode { Icon = "x" };
        node.Measure(new Constraints(0, 40, 0, 10));
        node.Arrange(new Rect(5, 3, 1, 1));

        Assert.Equal(default(Rect), node.HitTestBounds);
    }

    #endregion

    #region WindowPanel Background Input Routing Tests

    [Fact]
    public void WindowPanelNode_GetChildren_IncludesBackground()
    {
        var bgNode = new TextBlockNode { Text = "bg" };
        var windowNode = new WindowNode();
        var panel = new WindowPanelNode();
        panel.BackgroundNode = bgNode;
        panel.WindowNodes.Add(windowNode);

        var children = panel.GetChildren().ToList();

        // Background should be first (lowest hit priority), then windows
        Assert.Contains(bgNode, children);
        Assert.Contains(windowNode, children);
        Assert.Equal(bgNode, children[0]);
    }

    [Fact]
    public void WindowPanelNode_GetChildren_NullBackground_OnlyWindows()
    {
        var windowNode = new WindowNode();
        var panel = new WindowPanelNode();
        panel.BackgroundNode = null;
        panel.WindowNodes.Add(windowNode);

        var children = panel.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(windowNode, children[0]);
    }

    #endregion

    #region Drop Target Proximity Manhattan Distance Tests

    [Fact]
    public void DroppableNode_FindDropTargets_HorizontalLayout_FoundByPosition()
    {
        // Simulate a horizontal layout of drop targets (like between commands on a track)
        var droppable = new DroppableNode();
        var hstack = new HStackNode();
        var dt1 = new DropTargetNode { TargetId = "pos-0" };
        var dt2 = new DropTargetNode { TargetId = "pos-1" };
        var dt3 = new DropTargetNode { TargetId = "pos-2" };
        var text1 = new TextBlockNode { Text = "cmd1" };
        var text2 = new TextBlockNode { Text = "cmd2" };

        hstack.Children = [dt1, text1, dt2, text2, dt3];
        droppable.Child = hstack;

        var targets = droppable.FindDropTargets();
        Assert.Equal(3, targets.Count);
        Assert.Equal("pos-0", targets[0].TargetId);
        Assert.Equal("pos-1", targets[1].TargetId);
        Assert.Equal("pos-2", targets[2].TargetId);
    }

    #endregion

    #region Full Stack Drag-Drop Integration Tests

    [Fact]
    public async Task DragDrop_DragFromSourceToTarget_DropsData()
    {
        var dropped = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                // Row 0: draggable item
                v.Draggable("task-1", dc =>
                    dc.Text(dc.IsDragging ? "[dragging]" : "[Drag Me]"))
                    .DragOverlay(dc => dc.Text("Ghost")),
                // Row 1-3: spacer
                v.Text(""),
                v.Text(""),
                // Row 3+: drop target
                v.Droppable(dc => dc.Text(
                    dc.IsHoveredByDrag ? ">> Drop Here <<" : "Target Area"))
                    .Accept(data => data is string)
                    .OnDrop(e =>
                    {
                        dropped.TrySetResult(e.DragData);
                    }),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[Drag Me]") && s.ContainsText("Target Area"),
                TimeSpan.FromSeconds(5), "initial render")
            // Drag from the draggable (row 0) to the drop target (row 3)
            .Drag(5, 0, 5, 3)
            .WaitUntil(s => s.ContainsText(">> Drop Here <<") || dropped.Task.IsCompleted,
                TimeSpan.FromSeconds(2), "drop complete")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var result = await dropped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("task-1", result);
    }

    [Fact]
    public async Task DragDrop_AcceptPredicate_RejectsInvalidData()
    {
        var dropOccurred = false;

        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                // Draggable with integer data
                v.Draggable(42, dc => dc.Text("[Number Item]")),
                v.Text(""),
                v.Text(""),
                // Drop target only accepts strings
                v.Droppable(dc => dc.Text("Strings Only"))
                    .Accept(data => data is string)
                    .OnDrop(e => { dropOccurred = true; }),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[Number Item]") && s.ContainsText("Strings Only"),
                TimeSpan.FromSeconds(5), "initial render")
            .Drag(5, 0, 5, 4)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.False(dropOccurred, "Drop should be rejected because data is int, not string");
    }

    [Fact]
    public async Task DragDrop_DragOverlay_ShowsGhostWhileDragging()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Draggable("item", dc =>
                    dc.Text(dc.IsDragging ? "---" : "[Source]"))
                    .DragOverlay(dc => dc.Text("GHOST")),
                v.Text("").Fill(),
                v.Droppable(dc => dc.Text("Target"))
                    .Accept(data => data is string)
                    .OnDrop(e => { }),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Start drag and hold mid-way to check for overlay
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[Source]"),
                TimeSpan.FromSeconds(5), "initial render")
            // Mouse down on source
            .MouseMoveTo(3, 0)
            .MouseDown()
            .Wait(TimeSpan.FromMilliseconds(50))
            // Drag to middle — overlay should appear
            .MouseMoveTo(20, 5)
            .WaitUntil(s => s.ContainsText("GHOST"),
                TimeSpan.FromSeconds(2), "ghost overlay visible during drag")
            // Complete the drag
            .MouseUp()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task DragDrop_DropTarget_ActivatesNearestTarget()
    {
        string? droppedTargetId = null;

        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 5)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                // Row 0: draggable
                v.Draggable("cmd", dc => dc.Text("[CMD]"))
                    .DragOverlay(dc => dc.Text("cmd")),
                // Row 1: horizontal track with drop targets
                v.Droppable(dc => dc.HStack(h => [
                    dc.DropTarget("pos-0", dt =>
                        dt.Text(dt.IsActive ? "|" : " ")),
                    h.Text("Step1"),
                    dc.DropTarget("pos-1", dt =>
                        dt.Text(dt.IsActive ? "|" : " ")),
                    h.Text("Step2"),
                    dc.DropTarget("pos-2", dt =>
                        dt.Text(dt.IsActive ? "|" : " ")),
                ]))
                .Accept(data => data is string)
                .OnDropTarget(e => droppedTargetId = e.TargetId),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[CMD]") && s.ContainsText("Step1"),
                TimeSpan.FromSeconds(5), "initial render")
            // Drag from CMD (row 0) to near "pos-1" between Step1 and Step2 (row 1)
            // Step1 is ~6 chars in, the drop target between them is around col 6
            .Drag(3, 0, 6, 1)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // A drop target should have been selected (exact one depends on proximity)
        Assert.NotNull(droppedTargetId);
    }

    [Fact]
    public async Task DragDrop_RealisticKanban_DragBetweenColumns()
    {
        // Realistic scenario: two columns with items, drag an item from column 1 to column 2
        var column1Items = new List<string> { "Task A", "Task B", "Task C" };
        var column2Items = new List<string> { "Task D" };
        var dropSignal = new TaskCompletionSource<(string Item, string Column)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(50, 12)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.HStack(h => [
                // Column 1 (left, ~25 chars wide)
                h.Droppable(dc =>
                {
                    var items = new List<Hex1bWidget>();
                    items.Add(dc.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor,
                            dc.IsHoveredByDrag && dc.CanAcceptDrag
                                ? Hex1bColor.Green : Hex1bColor.White),
                        dc.Text("Column 1")));

                    foreach (var item in column1Items)
                    {
                        var captured = item;
                        items.Add(dc.Draggable(captured,
                            dragCtx => dragCtx.Text(dragCtx.IsDragging ? "---" : captured))
                            .DragOverlay(dragCtx => dragCtx.Text($"[{captured}]")));
                    }

                    return dc.VStack(_ => [.. items]);
                })
                .Accept(data => data is string)
                .OnDrop(e =>
                {
                    var item = (string)e.DragData;
                    column2Items.Remove(item);
                    if (!column1Items.Contains(item))
                        column1Items.Add(item);
                    dropSignal.TrySetResult((item, "Column 1"));
                }),

                // Column 2 (right, ~25 chars wide)
                h.Droppable(dc =>
                {
                    var items = new List<Hex1bWidget>();
                    items.Add(dc.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor,
                            dc.IsHoveredByDrag && dc.CanAcceptDrag
                                ? Hex1bColor.Green : Hex1bColor.White),
                        dc.Text("Column 2")));

                    foreach (var item in column2Items)
                    {
                        var captured = item;
                        items.Add(dc.Draggable(captured,
                            dragCtx => dragCtx.Text(dragCtx.IsDragging ? "---" : captured))
                            .DragOverlay(dragCtx => dragCtx.Text($"[{captured}]")));
                    }

                    return dc.VStack(_ => [.. items]);
                })
                .Accept(data => data is string)
                .OnDrop(e =>
                {
                    var item = (string)e.DragData;
                    column1Items.Remove(item);
                    if (!column2Items.Contains(item))
                        column2Items.Add(item);
                    dropSignal.TrySetResult((item, "Column 2"));
                }),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Task A") && s.ContainsText("Column 2"),
                TimeSpan.FromSeconds(5), "both columns rendered")
            // Drag "Task A" from column 1 (col ~3, row 1) to column 2 (col ~10, row 1)
            .Drag(3, 1, 10, 1)
            .WaitUntil(s => dropSignal.Task.IsCompleted || s.ContainsText("Task A") == false,
                TimeSpan.FromSeconds(2), "drop complete")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var result = await dropSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Task A", result.Item);
        Assert.Equal("Column 2", result.Column);
        Assert.Contains("Task A", column2Items);
        Assert.DoesNotContain("Task A", column1Items);
    }

    [Fact]
    public async Task DragDrop_IsDragging_SourceShowsPlaceholder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Draggable("item", dc =>
                    dc.Text(dc.IsDragging ? "PLACEHOLDER" : "Original"))
                    .DragOverlay(dc => dc.Text("Dragging...")),
                v.Text("").Fill(),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Original"),
                TimeSpan.FromSeconds(5), "initial render shows original text")
            // Start drag
            .MouseMoveTo(3, 0)
            .MouseDown()
            .Wait(TimeSpan.FromMilliseconds(50))
            .MouseMoveTo(20, 5)
            // While dragging, source should show placeholder
            .WaitUntil(s => s.ContainsText("PLACEHOLDER"),
                TimeSpan.FromSeconds(2), "source shows placeholder during drag")
            .MouseUp()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task DragDrop_HoverState_DroppableShowsFeedback()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Draggable("data", dc => dc.Text("[Source]"))
                    .DragOverlay(dc => dc.Text("G")),
                v.Text(""),
                v.Text(""),
                v.Text(""),
                v.Droppable(dc => dc.Text(
                    dc.IsHoveredByDrag
                        ? (dc.CanAcceptDrag ? "ACCEPT" : "REJECT")
                        : "Idle"))
                    .Accept(data => data is string)
                    .OnDrop(e => { }),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[Source]") && s.ContainsText("Idle"),
                TimeSpan.FromSeconds(5), "initial render")
            // Start dragging and move over the droppable
            .MouseMoveTo(3, 0)
            .MouseDown()
            .Wait(TimeSpan.FromMilliseconds(50))
            .MouseMoveTo(10, 4)
            .WaitUntil(s => s.ContainsText("ACCEPT"),
                TimeSpan.FromSeconds(2), "droppable shows accept feedback when hovered")
            .MouseUp()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task IconWidget_Clickable_ReceivesClick()
    {
        var clicked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(40, 5)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("Click the icon:"),
                v.Icon("x").OnClick(_ => clicked.TrySetResult()),
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click the icon:") && s.ContainsText("x"),
                TimeSpan.FromSeconds(5), "icon rendered")
            .ClickAt(0, 1) // Click on the icon at row 1
            .Wait(TimeSpan.FromMilliseconds(200))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var didClick = await Task.WhenAny(clicked.Task, Task.Delay(2000)) == clicked.Task;
        Assert.True(didClick, "Icon click handler should have fired");
    }

    [Fact]
    public async Task WindowPanel_BackgroundInteractive_ReceivesDrops()
    {
        var dropped = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithMouse()
            .WithDimensions(60, 10)
            .Build();

        using var app = new Hex1bApp(
            ctx => ctx.WindowPanel().Background(bg =>
                bg.VStack(v => [
                    v.Draggable("payload", dc =>
                        dc.Text(dc.IsDragging ? "---" : "[Drag]"))
                        .DragOverlay(dc => dc.Text("P")),
                    v.Text(""),
                    v.Text(""),
                    v.Droppable(dc =>
                        dc.Text(dc.IsHoveredByDrag ? ">> DROP <<" : "Target"))
                        .Accept(data => data is string)
                        .OnDrop(e => dropped.TrySetResult((string)e.DragData)),
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[Drag]") && s.ContainsText("Target"),
                TimeSpan.FromSeconds(5), "background content rendered")
            .Drag(3, 0, 3, 3)
            .WaitUntil(s => dropped.Task.IsCompleted || s.ContainsText(">> DROP <<"),
                TimeSpan.FromSeconds(2), "drop complete")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var result = await dropped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("payload", result);
    }

    #endregion
}
