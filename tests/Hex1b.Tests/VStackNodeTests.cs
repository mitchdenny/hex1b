using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for VStackNode layout and focus management.
/// </summary>
public class VStackNodeTests
{
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
        var child2 = new TextBlockNode { Text = "Fill" };
        var node = new VStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 },
            ChildHeightHints = new List<SizeHint> { SizeHint.Content, SizeHint.Fill }
        };
        
        node.Measure(Constraints.Tight(80, 10));
        node.Arrange(new Rect(0, 0, 80, 10));
        
        // First child should be content-sized (1 line)
        Assert.Equal(1, child1.Bounds.Height);
        // Second child should fill remaining space
        Assert.Equal(9, child2.Bounds.Height);
    }

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
        
        // Need to invalidate to refresh focusable cache
        node.InvalidateFocusCache();
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Tab, '\t', false, false, false));
        
        Assert.True(handled);
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
        node.InvalidateFocusCache();
        
        // First tab forward to set internal index
        node.HandleInput(new KeyInputEvent(ConsoleKey.Tab, '\t', false, false, false));
        // Then shift-tab back
        node.HandleInput(new KeyInputEvent(ConsoleKey.Tab, '\t', true, false, false));
        
        Assert.True(textBox1.IsFocused);
        Assert.False(textBox2.IsFocused);
    }

    [Fact]
    public void HandleInput_DispatchesToFocusedChild()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var textBox = new TextBoxNode { State = state, IsFocused = true };
        
        var node = new VStackNode { Children = new List<Hex1bNode> { textBox } };
        node.InvalidateFocusCache();
        
        node.HandleInput(new KeyInputEvent(ConsoleKey.X, 'X', false, false, false));
        
        Assert.Equal("helloX", state.Text);
    }

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
}
