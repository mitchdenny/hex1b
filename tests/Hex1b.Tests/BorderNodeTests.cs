using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for BorderNode layout, rendering, and input handling.
/// </summary>
public class BorderNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal)
    {
        return new Hex1bRenderContext(terminal);
    }

    [Fact]
    public void Measure_AddsBorderToChildSize()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new BorderNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // Child is 5 wide, 1 tall. Border adds 2 to each dimension.
        Assert.Equal(7, size.Width);
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_WithNoChild_ReturnsMinimalBorder()
    {
        var node = new BorderNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        // Just the border: 2 wide, 2 tall
        Assert.Equal(2, size.Width);
        Assert.Equal(2, size.Height);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var child = new TextBlockNode { Text = "This is a long text line" };
        var node = new BorderNode { Child = child };

        var size = node.Measure(new Constraints(0, 15, 0, 5));

        Assert.True(size.Width <= 15);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public void Arrange_PositionsChildInsideBorder()
    {
        var child = new TextBlockNode { Text = "Test" };
        var node = new BorderNode { Child = child };

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(new Rect(5, 3, 20, 10));

        // Child should be offset by 1 in each direction
        Assert.Equal(6, child.Bounds.X);
        Assert.Equal(4, child.Bounds.Y);
        // Child should be 2 smaller in each dimension
        Assert.Equal(18, child.Bounds.Width);
        Assert.Equal(8, child.Bounds.Height);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        var bounds = new Rect(0, 0, 20, 5);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Render_DrawsTopBorder()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var topLine = terminal.GetLineTrimmed(0);
        // Should contain top-left corner
        Assert.Contains("┌", topLine);
        // Should contain horizontal line
        Assert.Contains("─", topLine);
        // Should contain top-right corner
        Assert.Contains("┐", topLine);
    }

    [Fact]
    public void Render_DrawsBottomBorder()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var bottomLine = terminal.GetLineTrimmed(4);
        // Should contain bottom-left corner
        Assert.Contains("└", bottomLine);
        // Should contain bottom-right corner
        Assert.Contains("┘", bottomLine);
    }

    [Fact]
    public void Render_DrawsVerticalBorders()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        // Middle rows should have vertical borders
        var middleLine = terminal.GetLineTrimmed(2);
        Assert.Contains("│", middleLine);
    }

    [Fact]
    public void Render_WithTitle_ShowsTitleInTopBorder()
    {
        using var terminal = new Hex1bTerminal(30, 5);
        var context = CreateContext(terminal);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Content" },
            Title = "My Title"
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        var topLine = terminal.GetLineTrimmed(0);
        Assert.Contains("My Title", topLine);
    }

    [Fact]
    public void Render_RendersChildContent()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hello" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 15, 5));
        node.Render(context);

        Assert.Contains("Hello", terminal.GetScreenText());
    }

    [Fact]
    public void GetFocusableNodes_ReturnsFocusableChildren()
    {
        var textBox = new TextBoxNode { State = new TextBoxState() };
        var node = new BorderNode { Child = textBox };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(textBox, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNonFocusableChild_ReturnsEmpty()
    {
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var node = new BorderNode { Child = textBlock };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNoChild_ReturnsEmpty()
    {
        var node = new BorderNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void HandleInput_PassesToChild()
    {
        var state = new TextBoxState { Text = "test", CursorPosition = 4 };
        var textBox = new TextBoxNode { State = state, IsFocused = true };
        var node = new BorderNode { Child = textBox };

        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.A, 'A', false, false, false));

        Assert.True(handled);
        Assert.Equal("testA", state.Text);
    }

    [Fact]
    public void HandleInput_WithNoChild_ReturnsFalse()
    {
        var node = new BorderNode { Child = null };

        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.A, 'A', false, false, false));

        Assert.False(handled);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new BorderNode();

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void Render_WithCustomTheme_UsesThemeCharacters()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.TopLeftCorner, "╔")
            .Set(BorderTheme.TopRightCorner, "╗")
            .Set(BorderTheme.BottomLeftCorner, "╚")
            .Set(BorderTheme.BottomRightCorner, "╝")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.VerticalLine, "║");
        var context = new Hex1bRenderContext(terminal, theme);

        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var screenText = terminal.GetScreenText();
        Assert.Contains("╔", screenText);
        Assert.Contains("╗", screenText);
        Assert.Contains("╚", screenText);
        Assert.Contains("╝", screenText);
    }

    [Fact]
    public void GetFocusableNodes_WithNestedContainers_FindsAllFocusables()
    {
        var textBox1 = new TextBoxNode { State = new TextBoxState() };
        var textBox2 = new TextBoxNode { State = new TextBoxState() };
        var vstack = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, textBox2 }
        };
        var node = new BorderNode { Child = vstack };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Contains(textBox1, focusables);
        Assert.Contains(textBox2, focusables);
    }
}
