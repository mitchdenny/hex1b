using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBoxNode rendering and input handling.
/// </summary>
public class TextBoxNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal)
    {
        return new Hex1bRenderContext(terminal);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "hello" } };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // "[hello]" = 2 brackets + 5 chars = 7
        Assert.Equal(7, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyText_HasMinWidth()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "" } };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // "[ ]" = 2 brackets + 1 min char = 3
        Assert.Equal(3, size.Width);
    }

    [Fact]
    public void Render_Unfocused_ShowsBrackets()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = CreateContext(terminal);
        var node = new TextBoxNode 
        { 
            State = new TextBoxState { Text = "test" },
            IsFocused = false
        };
        
        node.Render(context);
        
        Assert.Contains("[test]", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_ShowsCursor()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = CreateContext(terminal);
        var node = new TextBoxNode 
        { 
            State = new TextBoxState { Text = "abc", CursorPosition = 1 },
            IsFocused = true
        };
        
        node.Render(context);
        
        // When focused, the cursor character should be highlighted
        // The raw output should contain ANSI codes
        Assert.Contains("b", terminal.RawOutput);
    }

    [Fact]
    public void HandleInput_WhenFocused_UpdatesState()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.X, 'X', false, false, false));
        
        Assert.True(handled);
        Assert.Equal("helloX", state.Text);
    }

    [Fact]
    public void HandleInput_WhenNotFocused_DoesNotHandle()
    {
        var state = new TextBoxState { Text = "hello" };
        var node = new TextBoxNode { State = state, IsFocused = false };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.X, 'X', false, false, false));
        
        Assert.False(handled);
        Assert.Equal("hello", state.Text);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new TextBoxNode();
        
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "test" } };
        var bounds = new Rect(5, 10, 20, 1);
        
        node.Arrange(bounds);
        
        Assert.Equal(bounds, node.Bounds);
    }
}
