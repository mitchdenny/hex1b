using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for VStackNode layout and focus management.
/// </summary>
public class VStackNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_SumsChildHeights()
    {
        var node = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" }
            }
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_TakesMaxWidth()
    {
        var node = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Short" },
                new TextBlockNode { Text = "A Much Longer Line" },
                new TextBlockNode { Text = "Medium" }
            }
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(18, size.Width);
    }

    [Fact]
    public void Measure_EmptyChildren_ReturnsZeroSize()
    {
        var node = new VStackNode { Children = new List<Hex1bNode>() };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxConstraints()
    {
        var node = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "This is a very long line of text" },
                new TextBlockNode { Text = "Another long line" }
            }
        };

        var size = node.Measure(new Constraints(0, 20, 0, 1));

        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_PositionsChildrenVertically()
    {
        var child1 = new TextBlockNode { Text = "Line 1" };
        var child2 = new TextBlockNode { Text = "Line 2" };
        var node = new VStackNode { Children = new List<Hex1bNode> { child1, child2 } };

        node.Measure(Constraints.Tight(80, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, child1.Bounds.Y);
        Assert.Equal(1, child2.Bounds.Y);
    }

    [Fact]
    public void Arrange_WithFillHints_DistributesSpace()
    {
        var child1 = new TextBlockNode { Text = "Fixed" };
        var child2 = new TextBlockNode { Text = "Fill", HeightHint = SizeHint.Fill };
        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 }
        };

        node.Measure(Constraints.Tight(80, 10));
        node.Arrange(new Rect(0, 0, 80, 10));

        // First child should be content-sized (1 line)
        Assert.Equal(1, child1.Bounds.Height);
        // Second child should fill remaining space
        Assert.Equal(9, child2.Bounds.Height);
    }

    [Fact]
    public void Arrange_WithFixedHints_UsesExactSize()
    {
        var child1 = new TextBlockNode { Text = "Fixed", HeightHint = SizeHint.Fixed(3) };
        var child2 = new TextBlockNode { Text = "Also Fixed", HeightHint = SizeHint.Fixed(5) };
        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 }
        };

        node.Measure(Constraints.Tight(80, 20));
        node.Arrange(new Rect(0, 0, 80, 20));

        Assert.Equal(3, child1.Bounds.Height);
        Assert.Equal(5, child2.Bounds.Height);
    }

    [Fact]
    public void Arrange_WithMixedHints_DistributesCorrectly()
    {
        var child1 = new TextBlockNode { Text = "Fixed", HeightHint = SizeHint.Fixed(2) };
        var child2 = new TextBlockNode { Text = "Fill 1", HeightHint = SizeHint.Fill };
        var child3 = new TextBlockNode { Text = "Fill 2", HeightHint = SizeHint.Fill };
        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { child1, child2, child3 }
        };

        node.Measure(Constraints.Tight(80, 12));
        node.Arrange(new Rect(0, 0, 80, 12));

        Assert.Equal(2, child1.Bounds.Height);
        // Remaining 10 units split between 2 fill children
        Assert.Equal(5, child2.Bounds.Height);
        Assert.Equal(5, child3.Bounds.Height);
    }

    [Fact]
    public void Arrange_AtOffset_PositionsChildrenCorrectly()
    {
        var child1 = new TextBlockNode { Text = "Line 1" };
        var child2 = new TextBlockNode { Text = "Line 2" };
        var node = new VStackNode { Children = new List<Hex1bNode> { child1, child2 } };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(5, 10, 30, 20));

        Assert.Equal(5, child1.Bounds.X);
        Assert.Equal(10, child1.Bounds.Y);
        Assert.Equal(5, child2.Bounds.X);
        Assert.Equal(11, child2.Bounds.Y);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_FindsAllFocusable()
    {
        var textBox1 = new TextBoxNode();
        var button = new ButtonNode { Label = "OK" };
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var textBox2 = new TextBoxNode();

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, button, textBlock, textBox2 }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(3, focusables.Count);
        Assert.Contains(textBox1, focusables);
        Assert.Contains(button, focusables);
        Assert.Contains(textBox2, focusables);
    }

    [Fact]
    public async Task HandleInput_Tab_MovesFocus()
    {
        var textBox1 = new TextBoxNode { IsFocused = true };
        var textBox2 = new TextBoxNode { IsFocused = false };

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, textBox2 }
        };

        // Use FocusRing for focus navigation (the new pattern)
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState = new InputRouterState();

        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.False(textBox1.IsFocused);
        Assert.True(textBox2.IsFocused);
    }

    [Fact]
    public async Task HandleInput_ShiftTab_MovesFocusBackward()
    {
        var textBox1 = new TextBoxNode { IsFocused = false };
        var textBox2 = new TextBoxNode { IsFocused = true };

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, textBox2 }
        };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState2 = new InputRouterState();

        // textBox2 starts focused at index 1, shift-tab moves back to index 0
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift), focusRing, routerState2, null, TestContext.Current.CancellationToken);

        Assert.True(textBox1.IsFocused);
        Assert.False(textBox2.IsFocused);
    }

    [Fact]
    public async Task HandleInput_DispatchesToFocusedChild()
    {
        var textBox = new TextBoxNode { Text = "hello", IsFocused = true };
        textBox.State.CursorPosition = 5;

        var node = new VStackNode { Children = new List<Hex1bNode> { textBox } };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState = new InputRouterState();

        // Use InputRouter to route input to the focused child
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal("helloX", textBox.Text);
    }

    [Fact]
    public async Task HandleInput_Tab_WrapsAroundToFirst()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = false };
        var button2 = new ButtonNode { Label = "2", IsFocused = true };

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState2 = new InputRouterState();

        // button2 starts focused at index 1, one Tab wraps to index 0
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing, routerState2, null, TestContext.Current.CancellationToken);

        Assert.True(button1.IsFocused);
        Assert.False(button2.IsFocused);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_RendersAllChildren()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);

        var node = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "First" },
                new TextBlockNode { Text = "Second" }
            }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        Assert.Contains("First", terminal.CreateSnapshot().GetScreenText());
        Assert.Contains("Second", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Render_ChildrenAppearOnDifferentLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);

        var node = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Line A" },
                new TextBlockNode { Text = "Line B" },
                new TextBlockNode { Text = "Line C" }
            }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        Assert.Equal("Line A", terminal.CreateSnapshot().GetLineTrimmed(0));
        Assert.Equal("Line B", terminal.CreateSnapshot().GetLineTrimmed(1));
        Assert.Equal("Line C", terminal.CreateSnapshot().GetLineTrimmed(2));
    }

    [Fact]
    public async Task Render_InNarrowTerminal_TextClipsAtEdge()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 10);

        await using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Text("LongTextHere")
            ]),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Long"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();
        
        // Text clips at terminal edge (not wraps)
        Assert.Equal("LongTextHe", snapshot.GetLineTrimmed(0));
        // Second line should be empty (no wrapping)
        Assert.Equal("", snapshot.GetLineTrimmed(1));
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_VStack_RendersMultipleChildren()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.Text("Body Content"),
                    v.Text("Footer")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Header"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Header"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Body Content"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Footer"));
    }

    [Fact]
    public async Task Integration_VStack_TabNavigatesThroughFocusables()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text1 = "";
        var text2 = "";
        var text3 = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text1).OnTextChanged(args => text1 = args.NewText),
                    v.Text("Non-focusable label"),
                    v.TextBox(text2).OnTextChanged(args => text2 = args.NewText),
                    v.TextBox(text3).OnTextChanged(args => text3 = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Type in first box, wait for it to appear, tab to second, type, etc.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("1")
            .WaitUntil(s => s.ContainsText("1"), TimeSpan.FromSeconds(2))
            .Tab()
            .Type("2")
            .WaitUntil(s => s.ContainsText("2"), TimeSpan.FromSeconds(2))
            .Tab()
            .Type("3")
            .WaitUntil(s => s.ContainsText("3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("1", text1);
        Assert.Equal("2", text2);
        Assert.Equal("3", text3);
    }

    [Fact]
    public async Task Integration_VStack_ShiftTabNavigatesBackward()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text1 = "";
        var text2 = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text1).OnTextChanged(args => text1 = args.NewText),
                    v.TextBox(text2).OnTextChanged(args => text2 = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Tab forward then shift-tab back - wait for any render to complete
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Tab()
            .Shift().Tab()
            .Type("A")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("A", text1);
        Assert.Equal("", text2);
    }

    [Fact]
    public async Task Integration_VStack_InNarrowTerminal_StillWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Short"),
                    v.Text("Medium text"),
                    v.Text("Very long text indeed")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Short"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Short"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Medium text"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Very long text"));
    }

    [Fact]
    public async Task Integration_VStack_WithMixedContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Title"),
                    v.TextBox("editable"),
                    v.Button("Submit").OnClick(_ => { clicked = true; return Task.CompletedTask; })
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Tab to button and click
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Title"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked);
        Assert.True(terminal.CreateSnapshot().ContainsText("Title"));
    }

    [Fact]
    public async Task Integration_VStack_NestedVStacks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Outer 1"),
                    v.VStack(inner => [
                        inner.Text("Inner 1"),
                        inner.Text("Inner 2")
                    ]),
                    v.Text("Outer 2")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Outer 1"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Outer 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Inner 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Inner 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Outer 2"));
    }

    [Fact]
    public async Task Integration_VStack_EmptyStack_DoesNotCrash()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => Array.Empty<Hex1bWidget>())
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // Empty stack - wait for alternate screen mode, then exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should complete without error
        Assert.False(terminal.CreateSnapshot().InAlternateScreen);
    }

    [Fact]
    public async Task Integration_VStack_DynamicContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var items = new List<string> { "Item 1", "Item 2", "Item 3" };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => items.Select(item => v.Text(item)).ToArray())
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Item 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Item 3"));
    }

    #endregion

    #region Orphan Tracking Tests

    [Fact]
    public void Reconcile_FewerChildren_TracksOrphanedBounds()
    {
        // Arrange - create a VStack with 5 children
        var initialWidget = new VStackWidget(new Hex1bWidget[]
        {
            new TextBlockWidget("Line 1"),
            new TextBlockWidget("Line 2"),
            new TextBlockWidget("Line 3"),
            new TextBlockWidget("Line 4"),
            new TextBlockWidget("Line 5"),
        });
        
        var context = ReconcileContext.CreateRoot();
        var node = initialWidget.ReconcileAsync(null, context).GetAwaiter().GetResult() as VStackNode;
        Assert.NotNull(node);
        Assert.Equal(5, node.Children.Count);
        
        // Set up bounds for each child (simulating what Arrange would do)
        node.Arrange(new Rect(0, 0, 50, 5));
        for (int i = 0; i < node.Children.Count; i++)
        {
            node.Children[i].Arrange(new Rect(0, i, 50, 1));
        }
        node.ClearDirty();
        foreach (var child in node.Children)
        {
            child.ClearDirty();
        }
        
        // Act - reconcile with fewer children
        var fewerWidget = new VStackWidget(new Hex1bWidget[]
        {
            new TextBlockWidget("Only one line"),
        });
        var reconciledNode = fewerWidget.ReconcileAsync(node, context).GetAwaiter().GetResult() as VStackNode;
        
        // Assert - orphaned bounds should be tracked
        Assert.Same(node, reconciledNode); // Same node reused
        Assert.Single(reconciledNode!.Children);
        Assert.NotNull(reconciledNode.OrphanedChildBounds);
        Assert.Equal(4, reconciledNode.OrphanedChildBounds.Count); // Children 1-4 were orphaned
        Assert.True(reconciledNode.IsDirty); // Should be marked dirty
        
        // Verify the orphaned bounds match the old children's positions
        Assert.Equal(new Rect(0, 1, 50, 1), reconciledNode.OrphanedChildBounds[0]);
        Assert.Equal(new Rect(0, 2, 50, 1), reconciledNode.OrphanedChildBounds[1]);
        Assert.Equal(new Rect(0, 3, 50, 1), reconciledNode.OrphanedChildBounds[2]);
        Assert.Equal(new Rect(0, 4, 50, 1), reconciledNode.OrphanedChildBounds[3]);
    }

    [Fact]
    public void Reconcile_SameOrMoreChildren_NoOrphanedBounds()
    {
        // Arrange - create a VStack with 2 children
        var initialWidget = new VStackWidget(new Hex1bWidget[]
        {
            new TextBlockWidget("Line 1"),
            new TextBlockWidget("Line 2"),
        });
        
        var context = ReconcileContext.CreateRoot();
        var node = initialWidget.ReconcileAsync(null, context).GetAwaiter().GetResult() as VStackNode;
        Assert.NotNull(node);
        node.Arrange(new Rect(0, 0, 50, 2));
        
        // Act - reconcile with same number of children
        var sameWidget = new VStackWidget(new Hex1bWidget[]
        {
            new TextBlockWidget("Updated Line 1"),
            new TextBlockWidget("Updated Line 2"),
        });
        var reconciledNode = sameWidget.ReconcileAsync(node, context).GetAwaiter().GetResult() as VStackNode;
        
        // Assert - no orphaned bounds
        Assert.Null(reconciledNode!.OrphanedChildBounds);
    }

    #endregion
}
