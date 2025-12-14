using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b.Tests;

public class InputRouterTests
{
    /// <summary>
    /// Mock focusable node for testing input routing.
    /// </summary>
    private sealed class MockFocusableNode : Hex1bNode
    {
        public override bool IsFocusable => true;
        private bool _isFocused;
        public override bool IsFocused { get => _isFocused; set => _isFocused = value; }
        
        public List<Hex1bKeyEvent> ReceivedInputs { get; } = new();
        
        public override Size Measure(Layout.Constraints constraints) => new Size(10, 1);
        public override void Render(Hex1bRenderContext context) { }
        
        public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
        {
            ReceivedInputs.Add(keyEvent);
            return InputResult.Handled;
        }
    }

    /// <summary>
    /// Mock container node for testing input routing.
    /// </summary>
    private sealed class MockContainerNode : Hex1bNode
    {
        public List<Hex1bNode> Children { get; } = new();
        
        public override Size Measure(Layout.Constraints constraints) => new Size(80, 24);
        public override void Render(Hex1bRenderContext context) { }
        
        public override IEnumerable<Hex1bNode> GetChildren() => Children;
        
        public override IEnumerable<Hex1bNode> GetFocusableNodes()
        {
            foreach (var child in Children)
            {
                foreach (var focusable in child.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
    }

    [Fact]
    public void RouteInput_ToFocusedNode_RoutesSuccessfully()
    {
        // Arrange
        var focusedNode = new MockFocusableNode { IsFocused = true };
        var container = new MockContainerNode();
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var keyEvent = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
        
        // Act
        var result = InputRouter.RouteInput(container, keyEvent);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Single(focusedNode.ReceivedInputs);
        Assert.Equal(Hex1bKey.A, focusedNode.ReceivedInputs[0].Key);
    }

    [Fact]
    public void RouteInput_WithMatchingBinding_ExecutesBinding()
    {
        // Arrange
        var bindingExecuted = false;
        var focusedNode = new MockFocusableNode { IsFocused = true };
        focusedNode.InputBindings = new[]
        {
            InputBinding.Ctrl(Hex1bKey.S, () => bindingExecuted = true, "Save")
        };
        
        var container = new MockContainerNode();
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var keyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.S);
        
        // Act
        var result = InputRouter.RouteInput(container, keyEvent);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(bindingExecuted);
        Assert.Empty(focusedNode.ReceivedInputs); // OnInput should not be called when binding matches
    }

    [Fact]
    public void RouteInput_ChildBindingOverridesParentBinding()
    {
        // Arrange
        var parentBindingExecuted = false;
        var childBindingExecuted = false;
        
        var focusedNode = new MockFocusableNode { IsFocused = true };
        focusedNode.InputBindings = new[]
        {
            InputBinding.Ctrl(Hex1bKey.S, () => childBindingExecuted = true, "Child Save")
        };
        
        var container = new MockContainerNode();
        container.InputBindings = new[]
        {
            InputBinding.Ctrl(Hex1bKey.S, () => parentBindingExecuted = true, "Parent Save")
        };
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var keyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.S);
        
        // Act
        var result = InputRouter.RouteInput(container, keyEvent);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(childBindingExecuted);
        Assert.False(parentBindingExecuted); // Child binding should win
    }

    [Fact]
    public void RouteInput_ParentBindingUsedWhenChildHasNone()
    {
        // Arrange
        var parentBindingExecuted = false;
        
        var focusedNode = new MockFocusableNode { IsFocused = true };
        // No bindings on focused node
        
        var container = new MockContainerNode();
        container.InputBindings = new[]
        {
            InputBinding.Ctrl(Hex1bKey.Q, () => parentBindingExecuted = true, "Quit")
        };
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var keyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.Q);
        
        // Act
        var result = InputRouter.RouteInput(container, keyEvent);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(parentBindingExecuted);
    }

    [Fact]
    public void RouteInput_NoFocusedNode_ReturnsNotHandled()
    {
        // Arrange
        var unfocusedNode = new MockFocusableNode { IsFocused = false };
        var container = new MockContainerNode();
        container.Children.Add(unfocusedNode);
        
        var keyEvent = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
        
        // Act
        var result = InputRouter.RouteInput(container, keyEvent);
        
        // Assert
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void RouteInput_NestedContainers_CollectsBindingsFromPath()
    {
        // Arrange - 3 levels: root -> middle -> focused
        var rootBindingExecuted = false;
        var middleBindingExecuted = false;
        
        var focusedNode = new MockFocusableNode { IsFocused = true };
        
        var middleContainer = new MockContainerNode();
        middleContainer.InputBindings = new[]
        {
            InputBinding.Ctrl(Hex1bKey.M, () => middleBindingExecuted = true, "Middle")
        };
        middleContainer.Children.Add(focusedNode);
        focusedNode.Parent = middleContainer;
        
        var rootContainer = new MockContainerNode();
        rootContainer.InputBindings = new[]
        {
            InputBinding.Ctrl(Hex1bKey.R, () => rootBindingExecuted = true, "Root")
        };
        rootContainer.Children.Add(middleContainer);
        middleContainer.Parent = rootContainer;
        
        // Act - trigger root binding
        var rootKeyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.R);
        var rootResult = InputRouter.RouteInput(rootContainer, rootKeyEvent);
        
        // Act - trigger middle binding
        var middleKeyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.M);
        var middleResult = InputRouter.RouteInput(rootContainer, middleKeyEvent);
        
        // Assert
        Assert.Equal(InputResult.Handled, rootResult);
        Assert.True(rootBindingExecuted);
        Assert.Equal(InputResult.Handled, middleResult);
        Assert.True(middleBindingExecuted);
    }

    [Fact]
    public void InputBinding_Factory_CreatesCorrectBindings()
    {
        // Test factory methods create correct key/modifier combinations
        var plainBinding = InputBinding.Plain(Hex1bKey.A, () => { });
        Assert.Equal(Hex1bKey.A, plainBinding.Key);
        Assert.Equal(Hex1bModifiers.None, plainBinding.Modifiers);
        
        var ctrlBinding = InputBinding.Ctrl(Hex1bKey.S, () => { });
        Assert.Equal(Hex1bKey.S, ctrlBinding.Key);
        Assert.Equal(Hex1bModifiers.Control, ctrlBinding.Modifiers);
        
        var altBinding = InputBinding.Alt(Hex1bKey.F, () => { });
        Assert.Equal(Hex1bKey.F, altBinding.Key);
        Assert.Equal(Hex1bModifiers.Alt, altBinding.Modifiers);
        
        var shiftBinding = InputBinding.Shift(Hex1bKey.Tab, () => { });
        Assert.Equal(Hex1bKey.Tab, shiftBinding.Key);
        Assert.Equal(Hex1bModifiers.Shift, shiftBinding.Modifiers);
        
        var ctrlShiftBinding = InputBinding.CtrlShift(Hex1bKey.Z, () => { });
        Assert.Equal(Hex1bKey.Z, ctrlShiftBinding.Key);
        Assert.Equal(Hex1bModifiers.Control | Hex1bModifiers.Shift, ctrlShiftBinding.Modifiers);
    }

    [Fact]
    public void Hex1bKeyEvent_Properties_ReturnCorrectValues()
    {
        var evt = new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control | Hex1bModifiers.Shift);
        
        Assert.True(evt.Control);
        Assert.True(evt.Shift);
        Assert.False(evt.Alt);
        Assert.True(evt.IsPrintable);
        
        var nonPrintable = new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None);
        Assert.False(nonPrintable.IsPrintable);
    }

    [Fact]
    public void KeyMapper_ToHex1bKey_MapsCorrectly()
    {
        Assert.Equal(Hex1bKey.A, KeyMapper.ToHex1bKey(ConsoleKey.A));
        Assert.Equal(Hex1bKey.Enter, KeyMapper.ToHex1bKey(ConsoleKey.Enter));
        Assert.Equal(Hex1bKey.Escape, KeyMapper.ToHex1bKey(ConsoleKey.Escape));
        Assert.Equal(Hex1bKey.Tab, KeyMapper.ToHex1bKey(ConsoleKey.Tab));
        Assert.Equal(Hex1bKey.UpArrow, KeyMapper.ToHex1bKey(ConsoleKey.UpArrow));
        Assert.Equal(Hex1bKey.F1, KeyMapper.ToHex1bKey(ConsoleKey.F1));
    }

    [Fact]
    public void KeyMapper_ToHex1bModifiers_MapsCorrectly()
    {
        Assert.Equal(Hex1bModifiers.None, KeyMapper.ToHex1bModifiers(false, false, false));
        Assert.Equal(Hex1bModifiers.Shift, KeyMapper.ToHex1bModifiers(true, false, false));
        Assert.Equal(Hex1bModifiers.Alt, KeyMapper.ToHex1bModifiers(false, true, false));
        Assert.Equal(Hex1bModifiers.Control, KeyMapper.ToHex1bModifiers(false, false, true));
        Assert.Equal(
            Hex1bModifiers.Shift | Hex1bModifiers.Control,
            KeyMapper.ToHex1bModifiers(true, false, true)
        );
    }
}
