using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

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
        
        // Action to configure bindings for this node
        public Action<InputBindingsBuilder>? BindingsConfig { get; set; }
        
        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
        {
            BindingsConfig?.Invoke(bindings);
        }
        
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
        
        // Action to configure bindings for this node
        public Action<InputBindingsBuilder>? BindingsConfig { get; set; }
        
        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
        {
            BindingsConfig?.Invoke(bindings);
        }
        
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
        focusedNode.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(() => bindingExecuted = true, "Save");
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
        focusedNode.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(() => childBindingExecuted = true, "Child Save");
        };
        
        var container = new MockContainerNode();
        container.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(() => parentBindingExecuted = true, "Parent Save");
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
        container.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.Q).Action(() => parentBindingExecuted = true, "Quit");
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
        middleContainer.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.M).Action(() => middleBindingExecuted = true, "Middle");
        };
        middleContainer.Children.Add(focusedNode);
        focusedNode.Parent = middleContainer;
        
        var rootContainer = new MockContainerNode();
        rootContainer.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.R).Action(() => rootBindingExecuted = true, "Root");
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
        // Test InputBindingsBuilder creates correct key/modifier combinations
        var builder = new InputBindingsBuilder();
        builder.Key(Hex1bKey.A).Action(() => { });
        builder.Ctrl().Key(Hex1bKey.S).Action(() => { });
        builder.Alt().Key(Hex1bKey.F).Action(() => { });
        builder.Shift().Key(Hex1bKey.Tab).Action(() => { });
        builder.Ctrl().Shift().Key(Hex1bKey.Z).Action(() => { });
        
        var bindings = builder.Bindings;
        
        Assert.Equal(5, bindings.Count);
        
        // Plain binding
        Assert.Equal(Hex1bKey.A, bindings[0].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.None, bindings[0].Steps[0].Modifiers);
        
        // Ctrl binding
        Assert.Equal(Hex1bKey.S, bindings[1].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.Control, bindings[1].Steps[0].Modifiers);
        
        // Alt binding
        Assert.Equal(Hex1bKey.F, bindings[2].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.Alt, bindings[2].Steps[0].Modifiers);
        
        // Shift binding
        Assert.Equal(Hex1bKey.Tab, bindings[3].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.Shift, bindings[3].Steps[0].Modifiers);
        
        // Ctrl+Shift binding
        Assert.Equal(Hex1bKey.Z, bindings[4].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.Control | Hex1bModifiers.Shift, bindings[4].Steps[0].Modifiers);
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

    [Fact]
    public void RouteInputToNode_TextBoxBackspace_DeletesAndPositionsCursor()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("hell", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public void RouteInputToNode_TextBoxDoubleBackspace_DeletesTwoCharacters()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - two backspaces
        InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));
        InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));

        // Assert
        Assert.Equal("hel", state.Text);
        Assert.Equal(3, state.CursorPosition);
    }

    #region CharacterBinding Tests

    [Fact]
    public void CharacterBinding_MatchesPrintableCharacter()
    {
        // Arrange
        char received = '\0';
        var binding = new CharacterBinding(c => !char.IsControl(c), c => received = c, "Insert char");

        // Act & Assert
        Assert.True(binding.Matches('a'));
        Assert.True(binding.Matches('Z'));
        Assert.True(binding.Matches('5'));
        Assert.True(binding.Matches(' '));
        Assert.False(binding.Matches('\n'));
        Assert.False(binding.Matches('\t'));
        Assert.False(binding.Matches('\b'));
        
        binding.Execute('X');
        Assert.Equal('X', received);
    }

    [Fact]
    public void CharacterBinding_CustomPredicate_MatchesDigitsOnly()
    {
        // Arrange
        char received = '\0';
        var binding = new CharacterBinding(char.IsDigit, c => received = c, "Insert digit");

        // Act & Assert
        Assert.True(binding.Matches('0'));
        Assert.True(binding.Matches('9'));
        Assert.False(binding.Matches('a'));
        Assert.False(binding.Matches(' '));
        
        binding.Execute('7');
        Assert.Equal('7', received);
    }

    [Fact]
    public void InputBindingsBuilder_AnyCharacter_CreatesCharacterBinding()
    {
        // Arrange
        var builder = new InputBindingsBuilder();
        char received = '\0';

        // Act
        builder.AnyCharacter().Action(c => received = c, "Type");

        // Assert
        Assert.Single(builder.CharacterBindings);
        Assert.Empty(builder.Bindings);  // Key bindings list should be empty
        
        var binding = builder.CharacterBindings[0];
        Assert.True(binding.Matches('a'));
        Assert.False(binding.Matches('\n'));
        Assert.Equal("Type", binding.Description);
    }

    [Fact]
    public void InputBindingsBuilder_Character_WithCustomPredicate()
    {
        // Arrange
        var builder = new InputBindingsBuilder();
        char received = '\0';

        // Act
        builder.Character(char.IsLetter).Action(c => received = c, "Letters only");

        // Assert
        Assert.Single(builder.CharacterBindings);
        
        var binding = builder.CharacterBindings[0];
        Assert.True(binding.Matches('a'));
        Assert.True(binding.Matches('Z'));
        Assert.False(binding.Matches('5'));
        Assert.False(binding.Matches(' '));
    }

    [Fact]
    public void InputBindingsBuilder_MixedBindings_KeyAndCharacter()
    {
        // Arrange
        var builder = new InputBindingsBuilder();

        // Act
        builder.Key(Hex1bKey.Enter).Action(() => { }, "Submit");
        builder.AnyCharacter().Action(_ => { }, "Type");

        // Assert
        Assert.Single(builder.Bindings);
        Assert.Single(builder.CharacterBindings);
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_InsertsCharacter()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None));

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("helloX", state.Text);
        Assert.Equal(6, state.CursorPosition);
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_MultipleCharacters()
    {
        // Arrange
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.H, 'H', Hex1bModifiers.None));
        InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.I, 'i', Hex1bModifiers.None));
        InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.None, '!', Hex1bModifiers.None));

        // Assert
        Assert.Equal("Hi!", state.Text);
        Assert.Equal(3, state.CursorPosition);
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_NotFocused_NotHandled()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = false };

        // Act
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None));

        // Assert
        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal("hello", state.Text);  // Text unchanged
    }

    [Fact]
    public void RouteInputToNode_KeyBindingTakesPrecedence_OverCharacterBinding()
    {
        // Arrange - node with both a key binding for 'A' and a character binding
        bool keyBindingCalled = false;
        char charReceived = '\0';
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Key(Hex1bKey.A).Action(() => keyBindingCalled = true, "A key");
            bindings.AnyCharacter().Action(c => charReceived = c, "Type");
        };

        // Act
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));

        // Assert - key binding should win
        Assert.Equal(InputResult.Handled, result);
        Assert.True(keyBindingCalled);
        Assert.Equal('\0', charReceived);  // Character binding should not have been called
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_FallsBackWhenNoKeyBinding()
    {
        // Arrange - node with key binding for Enter, but typing 'x'
        bool enterPressed = false;
        char charReceived = '\0';
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Key(Hex1bKey.Enter).Action(() => enterPressed = true, "Submit");
            bindings.AnyCharacter().Action(c => charReceived = c, "Type");
        };

        // Act
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.X, 'x', Hex1bModifiers.None));

        // Assert - character binding should handle it
        Assert.Equal(InputResult.Handled, result);
        Assert.False(enterPressed);
        Assert.Equal('x', charReceived);
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_NullCharacter_NotHandled()
    {
        // Arrange - key event with no character
        char charReceived = '\0';
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.AnyCharacter().Action(c => charReceived = c, "Type");
        };

        // Act - F1 key has no character
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.F1, '\0', Hex1bModifiers.None));

        // Assert - character binding should not match
        Assert.Equal(InputResult.Handled, result);  // Handled by HandleInput fallback
        Assert.Equal('\0', charReceived);
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_FirstMatchWins()
    {
        // Arrange - multiple character bindings, first matching one wins
        string handler = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Character(char.IsDigit).Action(c => handler = "digit", "Digits");
            bindings.AnyCharacter().Action(c => handler = "any", "Any char");
        };

        // Act - '5' matches both, but digit comes first
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.D5, '5', Hex1bModifiers.None));

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("digit", handler);
    }

    [Fact]
    public void RouteInputToNode_CharacterBinding_SecondMatchWhenFirstFails()
    {
        // Arrange - digit binding first, but typing a letter
        string handler = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Character(char.IsDigit).Action(c => handler = "digit", "Digits");
            bindings.AnyCharacter().Action(c => handler = "any", "Any char");
        };

        // Act - 'a' doesn't match digit, falls through to any
        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("any", handler);
    }

    [Fact]
    public void RouteInput_CharacterBinding_OnlyOnFocusedNode()
    {
        // Arrange - parent has character binding, child is focused
        char parentReceived = '\0';
        char childReceived = '\0';
        
        var container = new MockContainerNode();
        container.BindingsConfig = bindings =>
        {
            bindings.AnyCharacter().Action(c => parentReceived = c, "Parent type");
        };
        
        var child = new MockFocusableNode { IsFocused = true };
        child.BindingsConfig = bindings =>
        {
            bindings.AnyCharacter().Action(c => childReceived = c, "Child type");
        };
        
        container.Children.Add(child);
        child.Parent = container;

        // Act
        var result = InputRouter.RouteInput(container, new Hex1bKeyEvent(Hex1bKey.X, 'x', Hex1bModifiers.None));

        // Assert - only child's character binding should fire (not parent's)
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal('x', childReceived);
        Assert.Equal('\0', parentReceived);  // Parent's character binding should NOT have been called
    }

    #endregion
}
