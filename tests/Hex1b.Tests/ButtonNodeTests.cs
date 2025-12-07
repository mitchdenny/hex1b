using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ButtonNode rendering and input handling.
/// </summary>
public class ButtonNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal)
    {
        return new Hex1bRenderContext(terminal);
    }

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new ButtonNode { Label = "Click" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // "[ Click ]" = 4 (brackets + spaces) + 5 label = 9
        Assert.Equal(9, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyLabel_HasMinSize()
    {
        var node = new ButtonNode { Label = "" };
        
        var size = node.Measure(Constraints.Unbounded);
        
        // "[  ]" = 4
        Assert.Equal(4, size.Width);
    }

    [Fact]
    public void Render_Unfocused_ShowsBrackets()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = CreateContext(terminal);
        var node = new ButtonNode 
        { 
            Label = "OK",
            IsFocused = false
        };
        
        node.Render(context);
        
        // Theme-dependent bracket style, but should contain label
        Assert.Contains("OK", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_HasDifferentStyle()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = CreateContext(terminal);
        var node = new ButtonNode 
        { 
            Label = "OK",
            IsFocused = true
        };
        
        node.Render(context);
        
        // Should contain ANSI escape codes for focus styling
        Assert.Contains("\x1b[", terminal.RawOutput);
        Assert.Contains("OK", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void HandleInput_Enter_TriggersOnClick()
    {
        var clicked = false;
        var node = new ButtonNode 
        { 
            Label = "Click Me",
            IsFocused = true,
            OnClick = () => clicked = true
        };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false));
        
        Assert.True(handled);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_Space_TriggersOnClick()
    {
        var clicked = false;
        var node = new ButtonNode 
        { 
            Label = "Click Me",
            IsFocused = true,
            OnClick = () => clicked = true
        };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Spacebar, ' ', false, false, false));
        
        Assert.True(handled);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_OtherKey_DoesNotClick()
    {
        var clicked = false;
        var node = new ButtonNode 
        { 
            Label = "Click Me",
            IsFocused = true,
            OnClick = () => clicked = true
        };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.A, 'a', false, false, false));
        
        Assert.False(handled);
        Assert.False(clicked);
    }

    [Fact]
    public void HandleInput_NotFocused_DoesNotClick()
    {
        var clicked = false;
        var node = new ButtonNode 
        { 
            Label = "Click Me",
            IsFocused = false,
            OnClick = () => clicked = true
        };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false));
        
        Assert.False(handled);
        Assert.False(clicked);
    }

    [Fact]
    public void HandleInput_NullOnClick_DoesNotThrow()
    {
        var node = new ButtonNode 
        { 
            Label = "Click Me",
            IsFocused = true,
            OnClick = null
        };
        
        var handled = node.HandleInput(new KeyInputEvent(ConsoleKey.Enter, '\r', false, false, false));
        
        Assert.True(handled);
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new ButtonNode();
        
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new ButtonNode { Label = "Test" };
        var bounds = new Rect(0, 0, 20, 1);
        
        node.Arrange(bounds);
        
        Assert.Equal(bounds, node.Bounds);
    }
}
