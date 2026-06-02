using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class MouseBindingTests
{
    [TestMethod]
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
        
        Assert.IsTrue(binding.Matches(matchingEvent));
        Assert.IsFalse(binding.Matches(nonMatchingEvent));
    }
    
    [TestMethod]
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
        
        Assert.IsTrue(executed);
    }
    
    [TestMethod]
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
        
        Assert.IsTrue(binding.Matches(ctrlClick));
        Assert.IsFalse(binding.Matches(plainClick));
    }
    
    [TestMethod]
    public void InputBindingsBuilder_Mouse_AddsBinding()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Action(_ => { return Task.CompletedTask; }, "Click");
        
        TestSeq.Single(builder.MouseBindings);
        Assert.AreEqual(MouseButton.Left, builder.MouseBindings[0].Button);
        Assert.AreEqual(MouseAction.Down, builder.MouseBindings[0].Action);
        Assert.AreEqual("Click", builder.MouseBindings[0].Description);
    }
    
    [TestMethod]
    public void InputBindingsBuilder_Mouse_WithModifiers()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Ctrl().Action(_ => Task.CompletedTask, "Ctrl+Click");
        
        var binding = builder.MouseBindings[0];
        Assert.AreEqual(Hex1bModifiers.Control, binding.Modifiers);
    }
    
    [TestMethod]
    public void InputBindingsBuilder_Mouse_OnRelease()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).OnRelease().Action(_ => Task.CompletedTask, "Release");
        
        var binding = builder.MouseBindings[0];
        Assert.AreEqual(MouseAction.Up, binding.Action);
    }
    
    [TestMethod]
    public void InputBindingsBuilder_BuildsMultipleMouseBindings()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Action(_ => Task.CompletedTask, "Left");
        builder.Mouse(MouseButton.Right).Ctrl().Action(_ => Task.CompletedTask, "Ctrl+Right");
        builder.Mouse(MouseButton.Middle).OnRelease().Action(_ => Task.CompletedTask, "Middle Release");
        
        Assert.AreEqual(3, builder.MouseBindings.Count);
        
        Assert.AreEqual(MouseButton.Left, builder.MouseBindings[0].Button);
        Assert.AreEqual(MouseAction.Down, builder.MouseBindings[0].Action);
        Assert.AreEqual(Hex1bModifiers.None, builder.MouseBindings[0].Modifiers);
        
        Assert.AreEqual(MouseButton.Right, builder.MouseBindings[1].Button);
        Assert.AreEqual(MouseAction.Down, builder.MouseBindings[1].Action);
        Assert.AreEqual(Hex1bModifiers.Control, builder.MouseBindings[1].Modifiers);
        
        Assert.AreEqual(MouseButton.Middle, builder.MouseBindings[2].Button);
        Assert.AreEqual(MouseAction.Up, builder.MouseBindings[2].Action);
        Assert.AreEqual(Hex1bModifiers.None, builder.MouseBindings[2].Modifiers);
    }
}

[TestClass]
public class DoubleClickBindingTests
{
    [TestMethod]
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
        Assert.IsFalse(binding.Matches(singleClick));
        
        // Double click should match
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        Assert.IsTrue(binding.Matches(doubleClick));
        
        // Triple click should also match (>= 2)
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        Assert.IsTrue(binding.Matches(tripleClick));
    }
    
    [TestMethod]
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
        
        Assert.IsTrue(binding.Matches(singleClick));
        Assert.IsTrue(binding.Matches(doubleClick));
        Assert.IsTrue(binding.Matches(tripleClick));
    }
    
    [TestMethod]
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
        
        Assert.IsFalse(binding.Matches(singleClick));
        Assert.IsFalse(binding.Matches(doubleClick));
        Assert.IsTrue(binding.Matches(tripleClick));
    }
    
    [TestMethod]
    public void InputBindingsBuilder_DoubleClick_SetsClickCount()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).DoubleClick().Action(_ => Task.CompletedTask, "Double-click");
        
        var binding = builder.MouseBindings[0];
        Assert.AreEqual(2, binding.ClickCount);
    }
    
    [TestMethod]
    public void InputBindingsBuilder_TripleClick_SetsClickCount()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).TripleClick().Action(_ => Task.CompletedTask, "Triple-click");
        
        var binding = builder.MouseBindings[0];
        Assert.AreEqual(3, binding.ClickCount);
    }
    
    [TestMethod]
    public void InputBindingsBuilder_DoubleClick_WithModifiers()
    {
        var builder = new InputBindingsBuilder();
        
        builder.Mouse(MouseButton.Left).Ctrl().DoubleClick().Action(_ => Task.CompletedTask, "Ctrl+Double-click");
        
        var binding = builder.MouseBindings[0];
        Assert.AreEqual(Hex1bModifiers.Control, binding.Modifiers);
        Assert.AreEqual(2, binding.ClickCount);
        
        // Should match Ctrl+double-click
        var ctrlDoubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.Control, ClickCount: 2);
        Assert.IsTrue(binding.Matches(ctrlDoubleClick));
        
        // Should NOT match plain double-click
        var plainDoubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        Assert.IsFalse(binding.Matches(plainDoubleClick));
    }
    
    [TestMethod]
    public void Hex1bMouseEvent_ClickCount_DefaultsToOne()
    {
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None);
        Assert.AreEqual(1, mouseEvent.ClickCount);
    }
    
    [TestMethod]
    public void Hex1bMouseEvent_IsDoubleClick_ReturnsTrueForClickCountTwo()
    {
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        
        Assert.IsFalse(singleClick.IsDoubleClick);
        Assert.IsTrue(doubleClick.IsDoubleClick);
        Assert.IsFalse(tripleClick.IsDoubleClick);
    }
    
    [TestMethod]
    public void Hex1bMouseEvent_IsTripleClick_ReturnsTrueForClickCountThree()
    {
        var singleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 1);
        var doubleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 2);
        var tripleClick = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 10, Hex1bModifiers.None, ClickCount: 3);
        
        Assert.IsFalse(singleClick.IsTripleClick);
        Assert.IsFalse(doubleClick.IsTripleClick);
        Assert.IsTrue(tripleClick.IsTripleClick);
    }
    
    [TestMethod]
    public void Hex1bMouseEvent_WithClickCount_CreatesNewEvent()
    {
        var original = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 5, 10, Hex1bModifiers.Control);
        var modified = original.WithClickCount(2);
        
        // Original should be unchanged
        Assert.AreEqual(1, original.ClickCount);
        
        // Modified should have new click count but same other properties
        Assert.AreEqual(2, modified.ClickCount);
        Assert.AreEqual(MouseButton.Left, modified.Button);
        Assert.AreEqual(MouseAction.Down, modified.Action);
        Assert.AreEqual(5, modified.X);
        Assert.AreEqual(10, modified.Y);
        Assert.AreEqual(Hex1bModifiers.Control, modified.Modifiers);
    }
}

[TestClass]
public class FocusRingHitTestTests
{
    [TestMethod]
    public void HitTest_ReturnsNodeAtPosition()
    {
        var button = new ButtonNode { Label = "Test" };
        button.Arrange(new Rect(5, 5, 10, 1));
        button.IsFocused = true;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(button);
        
        // Hit inside bounds
        var hit = focusRing.HitTest(7, 5);
        Assert.AreSame(button, hit);
        
        // Miss - outside bounds
        var miss = focusRing.HitTest(0, 0);
        Assert.IsNull(miss);
    }
    
    [TestMethod]
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
        Assert.AreSame(button1, hit1);
        
        // Hit test on button2's row
        var hit2 = focusRing.HitTest(5, 1);
        Assert.AreSame(button2, hit2);
    }
    
    [TestMethod]
    public void HitTest_ReturnsNull_WhenNoFocusables()
    {
        var focusRing = new FocusRing();
        focusRing.Rebuild(null);
        
        var hit = focusRing.HitTest(5, 5);
        Assert.IsNull(hit);
    }
    
    // Simple test container that returns children as focusables
    private class TestContainerNode : Hex1bNode
    {
        private readonly Hex1bNode[] _children;
        
        public TestContainerNode(Hex1bNode[] children)
        {
            _children = children;
        }
        
        protected override Size MeasureCore(Constraints constraints) => new(10, _children.Length);
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

[TestClass]
public class HoverStateTests
{
    [TestMethod]
    public void IsHovered_DefaultsToFalse()
    {
        var button = new ButtonNode { Label = "Test" };
        Assert.IsFalse(button.IsHovered);
    }
    
    [TestMethod]
    public void IsHovered_CanBeSet()
    {
        var button = new ButtonNode { Label = "Test" };
        button.IsHovered = true;
        Assert.IsTrue(button.IsHovered);
        
        button.IsHovered = false;
        Assert.IsFalse(button.IsHovered);
    }
    
    [TestMethod]
    public void TextBoxNode_IsHovered_CanBeSet()
    {
        var textBox = new TextBoxNode { State = new TextBoxState { Text = "Hello" } };
        Assert.IsFalse(textBox.IsHovered);
        
        textBox.IsHovered = true;
        Assert.IsTrue(textBox.IsHovered);
    }
    
    [TestMethod]
    public void ListNode_IsHovered_CanBeSet()
    {
        var list = new ListNode { Items = ["Item 1", "Item 2"] };
        Assert.IsFalse(list.IsHovered);
        
        list.IsHovered = true;
        Assert.IsTrue(list.IsHovered);
    }
    
    [TestMethod]
    public void ToggleSwitchNode_IsHovered_CanBeSet()
    {
        var toggle = new ToggleSwitchNode { Options = ["A", "B"] };
        Assert.IsFalse(toggle.IsHovered);
        
        toggle.IsHovered = true;
        Assert.IsTrue(toggle.IsHovered);
    }
    
    [TestMethod]
    public void NonFocusableNode_IsHovered_ReturnsFalse()
    {
        // Create a simple non-focusable node
        var textBlock = new TextBlockNode { Text = "Hello" };
        Assert.IsFalse(textBlock.IsHovered);
        
        // Setting should be ignored (default implementation does nothing)
        textBlock.IsHovered = true;
        Assert.IsFalse(textBlock.IsHovered);
    }

    [TestMethod]
    public async Task MouseStepBuilder_TriggersActionId_AliasesPreviouslyRegisteredHandler()
    {
        // Mirror of the AgenticPromptDemo Ctrl+wheel scenario: a widget's
        // ConfigureDefaultBindings registers a handler against an ActionId
        // (plain ScrollUp). A user-supplied InputBindings configurator
        // then aliases the same ActionId to a different mouse trigger
        // (Ctrl+ScrollUp). Both bindings must coexist and route to the
        // same handler.
        var bindings = new InputBindingsBuilder();
        var actionId = new ActionId("Test.Scroll");
        var invocations = 0;

        // Widget default registers the action.
        bindings.Mouse(MouseButton.ScrollUp).Triggers(
            actionId,
            ctx => { invocations++; },
            "Scroll up");

        // User aliases it to Ctrl+ScrollUp without re-supplying the handler.
        bindings.Mouse(MouseButton.ScrollUp).Ctrl().Triggers(actionId);

        Assert.AreEqual(2, bindings.MouseBindings.Count);

        var plainBinding = bindings.MouseBindings[0];
        var ctrlBinding = bindings.MouseBindings[1];

        Assert.AreEqual(actionId, plainBinding.ActionId);
        Assert.AreEqual(actionId, ctrlBinding.ActionId);
        Assert.AreEqual(Hex1bModifiers.None, plainBinding.Modifiers);
        Assert.AreEqual(Hex1bModifiers.Control, ctrlBinding.Modifiers);
        Assert.AreEqual("Scroll up", ctrlBinding.Description);

        var ctx = new InputBindingActionContext(new FocusRing());
        var plainEvent = new Hex1bMouseEvent(MouseButton.ScrollUp, MouseAction.Down, 0, 0, Hex1bModifiers.None);
        var ctrlEvent = new Hex1bMouseEvent(MouseButton.ScrollUp, MouseAction.Down, 0, 0, Hex1bModifiers.Control);

        Assert.IsTrue(plainBinding.Matches(plainEvent));
        Assert.IsFalse(plainBinding.Matches(ctrlEvent));
        Assert.IsTrue(ctrlBinding.Matches(ctrlEvent));
        Assert.IsFalse(ctrlBinding.Matches(plainEvent));

        await plainBinding.ExecuteAsync(ctx);
        await ctrlBinding.ExecuteAsync(ctx);

        Assert.AreEqual(2, invocations);
    }

    [TestMethod]
    public void MouseStepBuilder_TriggersActionId_ThrowsWhenActionUnregistered()
    {
        var bindings = new InputBindingsBuilder();
        var unregistered = new ActionId("Test.NotRegistered");

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => bindings.Mouse(MouseButton.ScrollUp).Ctrl().Triggers(unregistered));
        Assert.Contains(unregistered.Value, ex.Message);
    }
}
