using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for HStackNode layout, rendering, and focus management.
/// </summary>
public class HStackNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_SumsChildWidths()
    {
        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "AAA" },
                new TextBlockNode { Text = "BBB" },
                new TextBlockNode { Text = "CCC" }
            }
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(9, size.Width);
    }

    [Fact]
    public void Measure_TakesMaxHeight()
    {
        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Short" },
                new TextBlockNode { Text = "Tall" },
                new TextBlockNode { Text = "Medium" }
            }
        };

        var size = node.Measure(Constraints.Unbounded);

        // All TextBlocks are 1 line tall
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyChildren_ReturnsZeroSize()
    {
        var node = new HStackNode { Children = new List<Hex1bNode>() };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxConstraints()
    {
        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "FirstLongWord" },
                new TextBlockNode { Text = "SecondLongWord" }
            }
        };

        var size = node.Measure(new Constraints(0, 15, 0, 5));

        Assert.Equal(15, size.Width);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_PositionsChildrenHorizontally()
    {
        var child1 = new TextBlockNode { Text = "AAA" };
        var child2 = new TextBlockNode { Text = "BBB" };
        var node = new HStackNode { Children = new List<Hex1bNode> { child1, child2 } };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, child1.Bounds.X);
        Assert.Equal(3, child2.Bounds.X);
    }

    [Fact]
    public void Arrange_WithFillHints_DistributesWidth()
    {
        var child1 = new TextBlockNode { Text = "A" };
        var child2 = new TextBlockNode { Text = "B", WidthHint = SizeHint.Fill };
        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 }
        };

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(new Rect(0, 0, 20, 10));

        // First child should be content-sized (1 char)
        Assert.Equal(1, child1.Bounds.Width);
        // Second child should fill remaining space
        Assert.Equal(19, child2.Bounds.Width);
    }

    [Fact]
    public void Arrange_WithFixedHints_UsesExactSize()
    {
        var child1 = new TextBlockNode { Text = "Fixed1", WidthHint = SizeHint.Fixed(10) };
        var child2 = new TextBlockNode { Text = "Fixed2", WidthHint = SizeHint.Fixed(15) };
        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        Assert.Equal(10, child1.Bounds.Width);
        Assert.Equal(15, child2.Bounds.Width);
    }

    [Fact]
    public void Arrange_WithMixedHints_DistributesCorrectly()
    {
        var child1 = new TextBlockNode { Text = "Fixed", WidthHint = SizeHint.Fixed(10) };
        var child2 = new TextBlockNode { Text = "Fill 1", WidthHint = SizeHint.Fill };
        var child3 = new TextBlockNode { Text = "Fill 2", WidthHint = SizeHint.Fill };
        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { child1, child2, child3 }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        Assert.Equal(10, child1.Bounds.Width);
        // Remaining 30 units split between 2 fill children
        Assert.Equal(15, child2.Bounds.Width);
        Assert.Equal(15, child3.Bounds.Width);
    }

    [Fact]
    public void Arrange_AtOffset_PositionsChildrenCorrectly()
    {
        var child1 = new TextBlockNode { Text = "AAA" };
        var child2 = new TextBlockNode { Text = "BBB" };
        var node = new HStackNode { Children = new List<Hex1bNode> { child1, child2 } };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(5, 10, 30, 20));

        Assert.Equal(5, child1.Bounds.X);
        Assert.Equal(10, child1.Bounds.Y);
        Assert.Equal(8, child2.Bounds.X);
        Assert.Equal(10, child2.Bounds.Y);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_FindsAllFocusable()
    {
        var button1 = new ButtonNode { Label = "1" };
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var button2 = new ButtonNode { Label = "2" };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, textBlock, button2 }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Contains(button1, focusables);
        Assert.Contains(button2, focusables);
    }

    [Fact]
    public async Task HandleInput_Tab_MovesFocus()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = true };
        var button2 = new ButtonNode { Label = "2", IsFocused = false };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState = new InputRouterState();

        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.False(button1.IsFocused);
        Assert.True(button2.IsFocused);
    }

    [Fact]
    public async Task HandleInput_ShiftTab_MovesFocusBackward()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = true };
        var button2 = new ButtonNode { Label = "2", IsFocused = false };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState2 = new InputRouterState();

        // Shift-tab from button1 should wrap to button2
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift), focusRing, routerState2, null, TestContext.Current.CancellationToken);

        Assert.False(button1.IsFocused);
        Assert.True(button2.IsFocused);
    }

    [Fact]
    public async Task HandleInput_DispatchesToFocusedChild()
    {
        var clicked = false;
        var button = new ButtonNode { Label = "Click", IsFocused = true, ClickAction = _ => { clicked = true; return Task.CompletedTask; } };

        var node = new HStackNode { Children = new List<Hex1bNode> { button } };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState = new InputRouterState();

        // Use InputRouter to route input to the focused child
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.True(clicked);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_RendersAllChildren()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);

        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Left" },
                new TextBlockNode { Text = "Right" }
            }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        Assert.Contains("Left", terminal.CreateSnapshot().GetScreenText());
        Assert.Contains("Right", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Render_ChildrenAppearOnSameLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);

        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "AAA" },
                new TextBlockNode { Text = "BBB" },
                new TextBlockNode { Text = "CCC" }
            }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        var line = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("AAA", line);
        Assert.Contains("BBB", line);
        Assert.Contains("CCC", line);
    }

    [Fact]
    public void Render_InNarrowTerminal_TextWraps()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 8, 10);
        var context = new Hex1bRenderContext(workload);

        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "AAAA" },
                new TextBlockNode { Text = "BBBB" }
            }
        };

        node.Measure(Constraints.Tight(8, 10));
        node.Arrange(new Rect(0, 0, 8, 10));
        node.Render(context);

        // Content wraps at terminal edge
        Assert.Contains("AAAA", terminal.CreateSnapshot().GetLine(0));
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_HStack_RendersMultipleChildren()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("Left"),
                    h.Text(" | "),
                    h.Text("Right")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Left"));
        Assert.True(terminal.CreateSnapshot().ContainsText("|"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right"));
    }

    [Fact]
    public async Task Integration_HStack_TabNavigatesThroughFocusables()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Button("Btn1").OnClick(_ => { button1Clicked = true; return Task.CompletedTask; }),
                    h.Text(" | "),
                    h.Button("Btn2").OnClick(_ => { button2Clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab to second button and click
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Btn1"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(button1Clicked);
        Assert.True(button2Clicked);
    }

    [Fact]
    public async Task Integration_HStack_InNarrowTerminal_StillWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("A"),
                    h.Text("B"),
                    h.Text("C")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("C"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("A"));
        Assert.True(terminal.CreateSnapshot().ContainsText("B"));
        Assert.True(terminal.CreateSnapshot().ContainsText("C"));
    }

    [Fact]
    public async Task Integration_HStack_WithVStack_NestedLayouts()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.VStack(v => [
                        v.Text("Left Top"),
                        v.Text("Left Bottom")
                    ]),
                    h.VStack(v => [
                        v.Text("Right Top"),
                        v.Text("Right Bottom")
                    ])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right Bottom"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Left Top"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Left Bottom"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right Top"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right Bottom"));
    }

    [Fact]
    public async Task Integration_HStack_WithMixedContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "";
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("Label: "),
                    h.TextBox(text).OnTextChanged(args => text = args.NewText),
                    h.Button("Submit").OnClick(_ => { clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type in textbox then tab to button and click
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Label:"), TimeSpan.FromSeconds(2))
            .Type("Hi")
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hi", text);
        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_HStack_EmptyStack_DoesNotCrash()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => Array.Empty<Hex1bWidget>())
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should complete without error - test is that we didn't throw
    }

    [Fact]
    public async Task Integration_HStack_LongContentInNarrowTerminal_Wraps()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 5);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("VeryLongWord"),
                    h.Text("AndAnother")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("VeryLongWord"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Content wraps at terminal boundary
        Assert.True(terminal.CreateSnapshot().ContainsText("VeryLongWord"));
    }

    [Fact]
    public async Task Integration_HStack_DynamicContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var items = new List<string> { "A", "B", "C" };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => items.Select(item => h.Text($"[{item}]")).ToArray())
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[C]"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("[A]"));
        Assert.True(terminal.CreateSnapshot().ContainsText("[B]"));
        Assert.True(terminal.CreateSnapshot().ContainsText("[C]"));
    }

    [Fact]
    public async Task Integration_TabFromSingleFocusableVStackInHStack_BubblesUpToHStack()
    {
        // Scenario: HStack with children that are VStacks, each containing only one focusable.
        // Tab should bubble up from the VStack to the HStack since there's nothing to cycle within.
        // This simulates the ResponsiveTodoExhibit Extra Wide layout.
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var rightButtonClicked = false;
        IReadOnlyList<string> items = ["Item 1", "Item 2"];

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Left VStack: only one focusable (List)
                    h.VStack(v => [v.Text("Header"), v.List(items)]),
                    // Right VStack: only one focusable (Button)
                    h.VStack(v => [v.Text("Actions"), v.Button("Add").OnClick(_ => { rightButtonClicked = true; return Task.CompletedTask; })])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // List starts focused; Tab should bubble up to HStack and move to Button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Add"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(rightButtonClicked, "Tab should have moved focus from List in left VStack to Button in right VStack");
    }

    [Fact]
    public async Task Integration_VStackWithMultipleFocusables_HandleTabInternally()
    {
        // Scenario: VStack with 2 focusables should handle Tab itself, not bubble up
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Button 1").OnClick(_ => { button1Clicked = true; return Task.CompletedTask; }),
                    v.Button("Button 2").OnClick(_ => { button2Clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Button 1 starts focused
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Button 1"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(button1Clicked);
        Assert.True(button2Clicked, "Tab should have moved focus from Button 1 to Button 2 within VStack");
    }

    [Fact]
    public async Task Integration_NestedVStackWithMultipleFocusables_TabEscapesAtBoundary()
    {
        // Scenario: VStack with 2 focusables nested inside an HStack.
        // Tab should cycle within the VStack, but escape at the last item.
        // This is the ResponsiveTodoExhibit "New" panel scenario.
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var listClicked = false;
        var addButtonClicked = false;
        var otherButtonClicked = false;
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Left: List
                    h.Button("List").OnClick(_ => { listClicked = true; return Task.CompletedTask; }),
                    // Middle: VStack with TextBox + Button (like "New" panel)
                    h.VStack(v => [
                        v.Text("New Task"),
                        v.TextBox(text).OnTextChanged(args => text = args.NewText),
                        v.Button("Add").OnClick(_ => { addButtonClicked = true; return Task.CompletedTask; })
                    ]),
                    // Right: Another button
                    h.Button("Other").OnClick(_ => { otherButtonClicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // List button starts focused
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("List"), TimeSpan.FromSeconds(2))
            .Tab()   // List -> TextBox (enters VStack)
            .Tab()   // TextBox -> Add (within VStack)
            .Tab()   // Add -> Other (escapes VStack at boundary!)
            .Enter() // Click Other button
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(listClicked);
        Assert.False(addButtonClicked);
        Assert.True(otherButtonClicked, "Tab at VStack boundary should escape to next HStack child");
    }

    [Fact]
    public async Task Integration_NestedVStackWithMultipleFocusables_ShiftTabEscapesAtBoundary()
    {
        // Scenario: Same as above but with Shift+Tab escaping at the first item.
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var listClicked = false;
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Left: Button
                    h.Button("List").OnClick(_ => { listClicked = true; return Task.CompletedTask; }),
                    // Right: VStack with TextBox + Button
                    h.VStack(v => [
                        v.Text("New Task"),
                        v.TextBox(text).OnTextChanged(args => text = args.NewText),
                        v.Button("Add").OnClick(_ => Task.CompletedTask)
                    ])
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // List button starts focused
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("List"), TimeSpan.FromSeconds(2))
            .Tab()                    // List -> TextBox (enters VStack)
            .Shift().Tab()            // TextBox -> List (escapes VStack!)
            .Enter()                  // Click List button
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(listClicked, "Shift+Tab at first VStack item should escape back to previous HStack child");
    }

    #endregion
}
