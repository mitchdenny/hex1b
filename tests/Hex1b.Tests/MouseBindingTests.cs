using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Tests;

public class MouseBindingTests
{
    [Fact]
    public void MouseBinding_MatchesCorrectEvent()
    {
        var executed = false;
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None, 
            () => executed = true, 
            "Test");
        
        var matchingEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        var nonMatchingEvent = new Hex1bMouseEvent(MouseButton.Right, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        
        Assert.True(binding.Matches(matchingEvent));
        Assert.False(binding.Matches(nonMatchingEvent));
    }
    
    [Fact]
    public void MouseBinding_Execute_InvokesHandler()
    {
        var executed = false;
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None, 
            () => executed = true, 
            "Test");
        
        binding.Execute();
        
        Assert.True(executed);
    }
    
    [Fact]
    public void MouseBinding_WithModifiers_MatchesCorrectly()
    {
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.Control, 
            () => { }, 
            "Ctrl+Click");
        
        var ctrlClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.Control);
        var plainClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        
        Assert.True(binding.Matches(ctrlClick));
        Assert.False(binding.Matches(plainClick));
    }
    
    [Fact]
    public void InputBindingsBuilder_Mouse_AddsBinding()
    {
        var builder = new InputBindingsBuilder();
        var executed = false;
        
        builder.Mouse(MouseButton.Left).Action(() => executed = true, "Click");
        
        Assert.Single(builder.MouseBindings);
        Assert.Equal(MouseButton.Left, builder.MouseBindings[0].Button);
        Assert.Equal(MouseAction.Down, builder.MouseBindings[0].Action);
        Assert.Equal("Click", builder.MouseBindings[0].Description);
    }
    
    [Fact]
    public void InputBindingsBuilder_Mouse_WithModifiers()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Ctrl().Shift().Action(() => { }, "Ctrl+Shift+Click");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(Hex1bModifiers.Control | Hex1bModifiers.Shift, binding.Modifiers);
    }
    
    [Fact]
    public void InputBindingsBuilder_Mouse_OnRelease()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).OnRelease().Action(() => { }, "Release");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(MouseAction.Up, binding.Action);
    }
    
    [Fact]
    public void InputBindingsBuilder_BuildsMultipleMouseBindings()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Action(() => { }, "Left");
        builder.Mouse(MouseButton.Right).Ctrl().Action(() => { }, "Ctrl+Right");
        builder.Mouse(MouseButton.Middle).OnRelease().Action(() => { }, "Middle Release");
        
        Assert.Equal(3, builder.MouseBindings.Count);
        
        Assert.Equal(MouseButton.Left, builder.MouseBindings[0].Button);
        Assert.Equal(MouseAction.Down, builder.MouseBindings[0].Action);
        Assert.Equal(Hex1bModifiers.None, builder.MouseBindings[0].Modifiers);
        
        Assert.Equal(MouseButton.Right, builder.MouseBindings[1].Button);
        Assert.Equal(MouseAction.Down, builder.MouseBindings[1].Action);
        Assert.Equal(Hex1bModifiers.Control, builder.MouseBindings[1].Modifiers);
        
        Assert.Equal(MouseButton.Middle, builder.MouseBindings[2].Button);
        Assert.Equal(MouseAction.Up, builder.MouseBindings[2].Action);
        Assert.Equal(Hex1bModifiers.None, builder.MouseBindings[2].Modifiers);
    }
}

public class FocusRingHitTestTests
{
    [Fact]
    public void HitTest_ReturnsNodeAtPosition()
    {
        var button = new ButtonNode { Label = "Test" };
        button.Arrange(new Rect(5, 5, 10, 1));
        button.IsFocused = true;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(button);
        
        // Hit inside bounds
        var hit = focusRing.HitTest(7, 5);
        Assert.Same(button, hit);
        
        // Miss - outside bounds
        var miss = focusRing.HitTest(0, 0);
        Assert.Null(miss);
    }
    
    [Fact]
    public void HitTest_ReturnsTopmostNode_WhenOverlapping()
    {
        // Create a VStack with two buttons
        var button1 = new ButtonNode { Label = "First" };
        var button2 = new ButtonNode { Label = "Second" };
        
        // Arrange them at different positions
        button1.Arrange(new Rect(0, 0, 10, 1));
        button2.Arrange(new Rect(0, 1, 10, 1));
        button1.IsFocused = true;
        
        // Create a simple container that returns both as focusables
        var container = new TestContainerNode([button1, button2]);
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        
        // Hit test on button1's row
        var hit1 = focusRing.HitTest(5, 0);
        Assert.Same(button1, hit1);
        
        // Hit test on button2's row
        var hit2 = focusRing.HitTest(5, 1);
        Assert.Same(button2, hit2);
    }
    
    [Fact]
    public void HitTest_ReturnsNull_WhenNoFocusables()
    {
        var focusRing = new FocusRing();
        focusRing.Rebuild(null);
        
        var hit = focusRing.HitTest(5, 5);
        Assert.Null(hit);
    }
    
    // Simple test container that returns children as focusables
    private class TestContainerNode : Hex1bNode
    {
        private readonly Hex1bNode[] _children;
        
        public TestContainerNode(Hex1bNode[] children)
        {
            _children = children;
        }
        
        public override Size Measure(Constraints constraints) => new(10, _children.Length);
        public override void Render(Hex1bRenderContext context) { }
        
        public override IEnumerable<Hex1bNode> GetFocusableNodes()
        {
            foreach (var child in _children)
            {
                foreach (var focusable in child.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
    }
}
