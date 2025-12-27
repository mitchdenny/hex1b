using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class MouseBindingTests
{
    [Fact]
    public void MouseBinding_MatchesCorrectEvent()
    {
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None,
            1,
            _ => { return Task.CompletedTask; }, 
            "Test");
        
        var matchingEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        var nonMatchingEvent = new Hex1bMouseEvent(MouseButton.Right, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        
        Assert.True(binding.Matches(matchingEvent));
        Assert.False(binding.Matches(nonMatchingEvent));
    }
    
    [Fact]
    public async Task MouseBinding_Execute_InvokesHandler()
    {
        var executed = false;
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None,
            1,
            _ => { executed = true; return Task.CompletedTask; }, 
            "Test");
        
        var actionContext = new InputBindingActionContext(new FocusRing());
        await binding.ExecuteAsync(actionContext);
        
        Assert.True(executed);
    }
    
    [Fact]
    public void MouseBinding_WithModifiers_MatchesCorrectly()
    {
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.Control,
            1,
            _ => Task.CompletedTask, 
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
        
        builder.Mouse(MouseButton.Left).Action(_ => { return Task.CompletedTask; }, "Click");
        
        Assert.Single(builder.MouseBindings);
        Assert.Equal(MouseButton.Left, builder.MouseBindings[0].Button);
        Assert.Equal(MouseAction.Down, builder.MouseBindings[0].Action);
        Assert.Equal("Click", builder.MouseBindings[0].Description);
    }
    
    [Fact]
    public void InputBindingsBuilder_Mouse_WithModifiers()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Ctrl().Action(_ => Task.CompletedTask, "Ctrl+Click");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(Hex1bModifiers.Control, binding.Modifiers);
    }
    
    [Fact]
    public void InputBindingsBuilder_Mouse_OnRelease()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).OnRelease().Action(_ => Task.CompletedTask, "Release");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(MouseAction.Up, binding.Action);
    }
    
    [Fact]
    public void InputBindingsBuilder_BuildsMultipleMouseBindings()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Action(_ => Task.CompletedTask, "Left");
        builder.Mouse(MouseButton.Right).Ctrl().Action(_ => Task.CompletedTask, "Ctrl+Right");
        builder.Mouse(MouseButton.Middle).OnRelease().Action(_ => Task.CompletedTask, "Middle Release");
        
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

public class DoubleClickBindingTests
{
    [Fact]
    public void MouseBinding_WithClickCount_MatchesCorrectEvent()
    {
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None,
            clickCount: 2,
            _ => Task.CompletedTask, 
            "Double-click");
        
        // Single click should NOT match a double-click binding
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        Assert.False(binding.Matches(singleClick));
        
        // Double click should match
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        Assert.True(binding.Matches(doubleClick));
        
        // Triple click should also match (>= 2)
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        Assert.True(binding.Matches(tripleClick));
    }
    
    [Fact]
    public void MouseBinding_SingleClick_MatchesAllClickCounts()
    {
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None,
            1,
            _ => Task.CompletedTask, 
            "Click");
        
        // Single-click binding (default) should match all click counts
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        
        Assert.True(binding.Matches(singleClick));
        Assert.True(binding.Matches(doubleClick));
        Assert.True(binding.Matches(tripleClick));
    }
    
    [Fact]
    public void MouseBinding_TripleClick_RequiresThreeClicks()
    {
        var binding = new MouseBinding(
            MouseButton.Left, 
            MouseAction.Down, 
            Hex1bModifiers.None,
            clickCount: 3,
            _ => Task.CompletedTask, 
            "Triple-click");
        
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        
        Assert.False(binding.Matches(singleClick));
        Assert.False(binding.Matches(doubleClick));
        Assert.True(binding.Matches(tripleClick));
    }
    
    [Fact]
    public void InputBindingsBuilder_DoubleClick_SetsClickCount()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).DoubleClick().Action(_ => Task.CompletedTask, "Double-click");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(2, binding.ClickCount);
    }
    
    [Fact]
    public void InputBindingsBuilder_TripleClick_SetsClickCount()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).TripleClick().Action(_ => Task.CompletedTask, "Triple-click");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(3, binding.ClickCount);
    }
    
    [Fact]
    public void InputBindingsBuilder_DoubleClick_WithModifiers()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Ctrl().DoubleClick().Action(_ => Task.CompletedTask, "Ctrl+Double-click");
        
        var binding = builder.MouseBindings[0];
        Assert.Equal(Hex1bModifiers.Control, binding.Modifiers);
        Assert.Equal(2, binding.ClickCount);
        
        // Should match Ctrl+double-click
        var ctrlDoubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.Control, ClickCount: 2);
        Assert.True(binding.Matches(ctrlDoubleClick));
        
        // Should NOT match plain double-click
        var plainDoubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        Assert.False(binding.Matches(plainDoubleClick));
    }
    
    [Fact]
    public void Hex1bMouseEvent_ClickCount_DefaultsToOne()
    {
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        Assert.Equal(1, mouseEvent.ClickCount);
    }
    
    [Fact]
    public void Hex1bMouseEvent_IsDoubleClick_ReturnsTrueForClickCountTwo()
    {
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        
        Assert.False(singleClick.IsDoubleClick);
        Assert.True(doubleClick.IsDoubleClick);
        Assert.False(tripleClick.IsDoubleClick);
    }
    
    [Fact]
    public void Hex1bMouseEvent_IsTripleClick_ReturnsTrueForClickCountThree()
    {
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        
        Assert.False(singleClick.IsTripleClick);
        Assert.False(doubleClick.IsTripleClick);
        Assert.True(tripleClick.IsTripleClick);
    }
    
    [Fact]
    public void Hex1bMouseEvent_WithClickCount_CreatesNewEvent()
    {
        var original = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 10, Hex1bModifiers.Control);
        var modified = original.WithClickCount(2);
        
        // Original should be unchanged
        Assert.Equal(1, original.ClickCount);
        
        // Modified should have new click count but same other properties
        Assert.Equal(2, modified.ClickCount);
        Assert.Equal(MouseButton.Left, modified.Button);
        Assert.Equal(MouseAction.Down, modified.Action);
        Assert.Equal(5, modified.X);
        Assert.Equal(10, modified.Y);
        Assert.Equal(Hex1bModifiers.Control, modified.Modifiers);
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

public class HoverStateTests
{
    [Fact]
    public void IsHovered_DefaultsToFalse()
    {
        var button = new ButtonNode { Label = "Test" };
        Assert.False(button.IsHovered);
    }
    
    [Fact]
    public void IsHovered_CanBeSet()
    {
        var button = new ButtonNode { Label = "Test" };
        button.IsHovered = true;
        Assert.True(button.IsHovered);
        
        button.IsHovered = false;
        Assert.False(button.IsHovered);
    }
    
    [Fact]
    public void TextBoxNode_IsHovered_CanBeSet()
    {
        var textBox = new TextBoxNode { State = new TextBoxState { Text = "Hello" } };
        Assert.False(textBox.IsHovered);
        
        textBox.IsHovered = true;
        Assert.True(textBox.IsHovered);
    }
    
    [Fact]
    public void ListNode_IsHovered_CanBeSet()
    {
        var list = new ListNode { Items = ["Item 1", "Item 2"] };
        Assert.False(list.IsHovered);
        
        list.IsHovered = true;
        Assert.True(list.IsHovered);
    }
    
    [Fact]
    public void ToggleSwitchNode_IsHovered_CanBeSet()
    {
        var toggle = new ToggleSwitchNode { Options = ["A", "B"] };
        Assert.False(toggle.IsHovered);
        
        toggle.IsHovered = true;
        Assert.True(toggle.IsHovered);
    }
    
    [Fact]
    public void NonFocusableNode_IsHovered_ReturnsFalse()
    {
        // Create a simple non-focusable node
        var textBlock = new TextBlockNode { Text = "Hello" };
        Assert.False(textBlock.IsHovered);
        
        // Setting should be ignored (default implementation does nothing)
        textBlock.IsHovered = true;
        Assert.False(textBlock.IsHovered);
    }
}
