using Hex1b.Input;

namespace Hex1b.Tests;

public class MouseParserTests
{
    [Fact]
    public void TryParseSgr_MouseMove_ParsesCorrectly()
    {
        // Mouse move at position (10, 5) - SGR format: \e[<35;11;6M (1-based coords)
        // 35 = 32 (motion) + 3 (no button)
        var result = MouseParser.TryParseSgr("35;11;6M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.None, evt.Button);
        Assert.Equal(MouseAction.Move, evt.Action);
        Assert.Equal(10, evt.X); // 0-based
        Assert.Equal(5, evt.Y);  // 0-based
        Assert.Equal(Hex1bModifiers.None, evt.Modifiers);
    }
    
    [Fact]
    public void TryParseSgr_LeftButtonDown_ParsesCorrectly()
    {
        // Left button down at (1, 1) - SGR format: \e[<0;2;2M
        var result = MouseParser.TryParseSgr("0;2;2M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Left, evt.Button);
        Assert.Equal(MouseAction.Down, evt.Action);
        Assert.Equal(1, evt.X);
        Assert.Equal(1, evt.Y);
    }
    
    [Fact]
    public void TryParseSgr_LeftButtonUp_ParsesCorrectly()
    {
        // Left button up at (1, 1) - SGR format: \e[<0;2;2m (lowercase m = release)
        var result = MouseParser.TryParseSgr("0;2;2m", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Left, evt.Button);
        Assert.Equal(MouseAction.Up, evt.Action);
    }
    
    [Fact]
    public void TryParseSgr_RightButtonDown_ParsesCorrectly()
    {
        // Right button = 2
        var result = MouseParser.TryParseSgr("2;5;10M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Right, evt.Button);
        Assert.Equal(MouseAction.Down, evt.Action);
        Assert.Equal(4, evt.X);
        Assert.Equal(9, evt.Y);
    }
    
    [Fact]
    public void TryParseSgr_MiddleButtonDown_ParsesCorrectly()
    {
        // Middle button = 1
        var result = MouseParser.TryParseSgr("1;5;10M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Middle, evt.Button);
        Assert.Equal(MouseAction.Down, evt.Action);
    }
    
    [Fact]
    public void TryParseSgr_ScrollUp_ParsesCorrectly()
    {
        // Scroll up = 64 (scroll bit) + 0 (up)
        var result = MouseParser.TryParseSgr("64;10;5M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.ScrollUp, evt.Button);
        Assert.Equal(MouseAction.Down, evt.Action);
    }
    
    [Fact]
    public void TryParseSgr_ScrollDown_ParsesCorrectly()
    {
        // Scroll down = 64 (scroll bit) + 1 (down)
        var result = MouseParser.TryParseSgr("65;10;5M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.ScrollDown, evt.Button);
        Assert.Equal(MouseAction.Down, evt.Action);
    }
    
    [Fact]
    public void TryParseSgr_LeftDrag_ParsesCorrectly()
    {
        // Left button drag = 32 (motion) + 0 (left button)
        var result = MouseParser.TryParseSgr("32;15;20M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Left, evt.Button);
        Assert.Equal(MouseAction.Drag, evt.Action);
        Assert.Equal(14, evt.X);
        Assert.Equal(19, evt.Y);
    }
    
    [Fact]
    public void TryParseSgr_WithShiftModifier_ParsesCorrectly()
    {
        // Left button + Shift = 0 + 4
        var result = MouseParser.TryParseSgr("4;5;5M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Left, evt.Button);
        Assert.Equal(Hex1bModifiers.Shift, evt.Modifiers);
    }
    
    [Fact]
    public void TryParseSgr_WithAltModifier_ParsesCorrectly()
    {
        // Left button + Alt = 0 + 8
        var result = MouseParser.TryParseSgr("8;5;5M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Left, evt.Button);
        Assert.Equal(Hex1bModifiers.Alt, evt.Modifiers);
    }
    
    [Fact]
    public void TryParseSgr_WithControlModifier_ParsesCorrectly()
    {
        // Left button + Ctrl = 0 + 16
        var result = MouseParser.TryParseSgr("16;5;5M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(MouseButton.Left, evt.Button);
        Assert.Equal(Hex1bModifiers.Control, evt.Modifiers);
    }
    
    [Fact]
    public void TryParseSgr_InvalidSequence_ReturnsFalse()
    {
        Assert.False(MouseParser.TryParseSgr("invalid", out _));
        Assert.False(MouseParser.TryParseSgr("", out _));
        Assert.False(MouseParser.TryParseSgr("1;2M", out _)); // Missing y
        Assert.False(MouseParser.TryParseSgr("1;2;3X", out _)); // Wrong terminator
    }
    
    [Fact]
    public void TryParseSgr_LargeCoordinates_ParsesCorrectly()
    {
        // SGR mode supports coordinates > 223 (unlike legacy modes)
        var result = MouseParser.TryParseSgr("0;300;200M", out var evt);
        
        Assert.True(result);
        Assert.NotNull(evt);
        Assert.Equal(299, evt.X); // 0-based
        Assert.Equal(199, evt.Y);
    }
}
