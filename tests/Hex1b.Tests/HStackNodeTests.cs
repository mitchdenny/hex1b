using Hex1b.Layout;
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
    public void HandleInput_Tab_MovesFocus()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = true };
        var button2 = new ButtonNode { Label = "2", IsFocused = false };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };
        node.InvalidateFocusCache();

        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Tab, '\t', false, false, false));

        Assert.True(handled);
        Assert.False(button1.IsFocused);
        Assert.True(button2.IsFocused);
    }

    [Fact]
    public void HandleInput_ShiftTab_MovesFocusBackward()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = false };
        var button2 = new ButtonNode { Label = "2", IsFocused = true };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };
        node.InvalidateFocusCache();

        // Tab forward then shift-tab back
        node.HandleInput(new KeyInputEvent(ConsoleKey.Tab, '\t', false, false, false));
        node.HandleInput(new KeyInputEvent(ConsoleKey.Tab, '\t', true, false, false));

        Assert.True(button1.IsFocused);
        Assert.False(button2.IsFocused);
    }

    [Fact]
    public void HandleInput_DispatchesToFocusedChild()
    {
        var clicked = false;
        var button = new ButtonNode { Label = "Click", IsFocused = true, OnClick = () => clicked = true };

        var node = new HStackNode { Children = new List<Hex1bNode> { button } };
        node.InvalidateFocusCache();

        node.HandleInput(new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false));

        Assert.True(clicked);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_RendersAllChildren()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);

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

        Assert.Contains("Left", terminal.GetScreenText());
        Assert.Contains("Right", terminal.GetScreenText());
    }

    [Fact]
    public void Render_ChildrenAppearOnSameLine()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);

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

        var line = terminal.GetLineTrimmed(0);
        Assert.Contains("AAA", line);
        Assert.Contains("BBB", line);
        Assert.Contains("CCC", line);
    }

    [Fact]
    public void Render_InNarrowTerminal_TextWraps()
    {
        using var terminal = new Hex1bTerminal(8, 10);
        var context = new Hex1bRenderContext(terminal);

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
        Assert.Contains("AAAA", terminal.GetLine(0));
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_HStack_RendersMultipleChildren()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("Left"),
                    h.Text(" | "),
                    h.Text("Right")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Left"));
        Assert.True(terminal.ContainsText("|"));
        Assert.True(terminal.ContainsText("Right"));
    }

    [Fact]
    public async Task Integration_HStack_TabNavigatesThroughFocusables()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Button("Btn1", () => button1Clicked = true),
                    h.Text(" | "),
                    h.Button("Btn2", () => button2Clicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab to second button and click
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.False(button1Clicked);
        Assert.True(button2Clicked);
    }

    [Fact]
    public async Task Integration_HStack_InNarrowTerminal_StillWorks()
    {
        using var terminal = new Hex1bTerminal(20, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("A"),
                    h.Text("B"),
                    h.Text("C")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("A"));
        Assert.True(terminal.ContainsText("B"));
        Assert.True(terminal.ContainsText("C"));
    }

    [Fact]
    public async Task Integration_HStack_WithVStack_NestedLayouts()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Left Top"));
        Assert.True(terminal.ContainsText("Left Bottom"));
        Assert.True(terminal.ContainsText("Right Top"));
        Assert.True(terminal.ContainsText("Right Bottom"));
    }

    [Fact]
    public async Task Integration_HStack_WithMixedContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "" };
        var clicked = false;

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("Label: "),
                    h.TextBox(textState),
                    h.Button("Submit", () => clicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Type in textbox then tab to button and click
        terminal.SendKey(ConsoleKey.H, 'H', shift: true);
        terminal.SendKey(ConsoleKey.I, 'i');
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("Hi", textState.Text);
        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_HStack_EmptyStack_DoesNotCrash()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => Array.Empty<Hex1bWidget>())
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Should complete without error
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task Integration_HStack_LongContentInNarrowTerminal_Wraps()
    {
        using var terminal = new Hex1bTerminal(15, 5);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("VeryLongWord"),
                    h.Text("AndAnother")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Content wraps at terminal boundary
        Assert.True(terminal.ContainsText("VeryLongWord"));
    }

    [Fact]
    public async Task Integration_HStack_DynamicContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var items = new List<string> { "A", "B", "C" };

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => items.Select(item => h.Text($"[{item}]")).ToArray())
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("[A]"));
        Assert.True(terminal.ContainsText("[B]"));
        Assert.True(terminal.ContainsText("[C]"));
    }

    #endregion
}
