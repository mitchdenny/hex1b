using Hex1b.Input;
using Hex1b.Layout;
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
        var textBox1 = new TextBoxNode { State = new TextBoxState() };
        var button = new ButtonNode { Label = "OK" };
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var textBox2 = new TextBoxNode { State = new TextBoxState() };

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
    public void HandleInput_Tab_MovesFocus()
    {
        var textBox1 = new TextBoxNode { State = new TextBoxState(), IsFocused = true };
        var textBox2 = new TextBoxNode { State = new TextBoxState(), IsFocused = false };

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, textBox2 }
        };

        // Use FocusRing for focus navigation (the new pattern)
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);

        var result = InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing);

        Assert.Equal(InputResult.Handled, result);
        Assert.False(textBox1.IsFocused);
        Assert.True(textBox2.IsFocused);
    }

    [Fact]
    public void HandleInput_ShiftTab_MovesFocusBackward()
    {
        var textBox1 = new TextBoxNode { State = new TextBoxState(), IsFocused = false };
        var textBox2 = new TextBoxNode { State = new TextBoxState(), IsFocused = true };

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, textBox2 }
        };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);

        // textBox2 starts focused at index 1, shift-tab moves back to index 0
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift), focusRing);

        Assert.True(textBox1.IsFocused);
        Assert.False(textBox2.IsFocused);
    }

    [Fact]
    public void HandleInput_DispatchesToFocusedChild()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var textBox = new TextBoxNode { State = state, IsFocused = true };

        var node = new VStackNode { Children = new List<Hex1bNode> { textBox } };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);

        // Use InputRouter to route input to the focused child
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), focusRing);

        Assert.Equal("helloX", state.Text);
    }

    [Fact]
    public void HandleInput_Tab_WrapsAroundToFirst()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = false };
        var button2 = new ButtonNode { Label = "2", IsFocused = true };

        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);

        // button2 starts focused at index 1, one Tab wraps to index 0
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing);

        Assert.True(button1.IsFocused);
        Assert.False(button2.IsFocused);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_RendersAllChildren()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);

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

        Assert.Contains("First", terminal.GetScreenText());
        Assert.Contains("Second", terminal.GetScreenText());
    }

    [Fact]
    public void Render_ChildrenAppearOnDifferentLines()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);

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

        Assert.Equal("Line A", terminal.GetLineTrimmed(0));
        Assert.Equal("Line B", terminal.GetLineTrimmed(1));
        Assert.Equal("Line C", terminal.GetLineTrimmed(2));
    }

    [Fact]
    public void Render_InNarrowTerminal_TextWrapsAtEdge()
    {
        using var terminal = new Hex1bTerminal(10, 10);
        var context = new Hex1bRenderContext(terminal);

        var node = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "LongTextHere" }
            }
        };

        node.Measure(Constraints.Tight(10, 10));
        node.Arrange(new Rect(0, 0, 10, 10));
        node.Render(context);

        // Text wraps at terminal edge
        Assert.Equal("LongTextHe", terminal.GetLine(0));
        Assert.Equal("re", terminal.GetLineTrimmed(1));
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_VStack_RendersMultipleChildren()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.Text("Body Content"),
                    v.Text("Footer")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Header"));
        Assert.True(terminal.ContainsText("Body Content"));
        Assert.True(terminal.ContainsText("Footer"));
    }

    [Fact]
    public async Task Integration_VStack_TabNavigatesThroughFocusables()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var state1 = new TextBoxState { Text = "" };
        var state2 = new TextBoxState { Text = "" };
        var state3 = new TextBoxState { Text = "" };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(state1),
                    v.Text("Non-focusable label"),
                    v.TextBox(state2),
                    v.TextBox(state3)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Type in first box
        terminal.SendKey(ConsoleKey.D1, '1');
        // Tab to second
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.D2, '2');
        // Tab to third
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.D3, '3');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("1", state1.Text);
        Assert.Equal("2", state2.Text);
        Assert.Equal("3", state3.Text);
    }

    [Fact]
    public async Task Integration_VStack_ShiftTabNavigatesBackward()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var state1 = new TextBoxState { Text = "" };
        var state2 = new TextBoxState { Text = "" };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(state1),
                    v.TextBox(state2)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab forward then shift-tab back
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Tab, '\t', shift: true);
        terminal.SendKey(ConsoleKey.A, 'A', shift: true);
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("A", state1.Text);
        Assert.Equal("", state2.Text);
    }

    [Fact]
    public async Task Integration_VStack_InNarrowTerminal_StillWorks()
    {
        using var terminal = new Hex1bTerminal(15, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Short"),
                    v.Text("Medium text"),
                    v.Text("Very long text indeed")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Short"));
        Assert.True(terminal.ContainsText("Medium text"));
        Assert.True(terminal.ContainsText("Very long text"));
    }

    [Fact]
    public async Task Integration_VStack_WithMixedContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "editable" };
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Title"),
                    v.TextBox(textState),
                    v.Button("Submit", () => clicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab to button and click
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.True(clicked);
        Assert.True(terminal.ContainsText("Title"));
    }

    [Fact]
    public async Task Integration_VStack_NestedVStacks()
    {
        using var terminal = new Hex1bTerminal(80, 24);

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
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Outer 1"));
        Assert.True(terminal.ContainsText("Inner 1"));
        Assert.True(terminal.ContainsText("Inner 2"));
        Assert.True(terminal.ContainsText("Outer 2"));
    }

    [Fact]
    public async Task Integration_VStack_EmptyStack_DoesNotCrash()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => Array.Empty<Hex1bWidget>())
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Should complete without error
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task Integration_VStack_DynamicContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var items = new List<string> { "Item 1", "Item 2", "Item 3" };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => items.Select(item => v.Text(item)).ToArray())
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Item 1"));
        Assert.True(terminal.ContainsText("Item 2"));
        Assert.True(terminal.ContainsText("Item 3"));
    }

    #endregion
}
