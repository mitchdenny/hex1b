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
    public async Task RouteInput_ToFocusedNode_RoutesSuccessfully()
    {
        // Arrange
        var focusedNode = new MockFocusableNode { IsFocused = true };
        var container = new MockContainerNode();
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();
        var state = new InputRouterState();
        
        var keyEvent = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
        
        // Act
        var result = await InputRouter.RouteInputAsync(container, keyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Single(focusedNode.ReceivedInputs);
        Assert.Equal(Hex1bKey.A, focusedNode.ReceivedInputs[0].Key);
    }

    [Fact]
    public async Task RouteInput_WithMatchingBinding_ExecutesBinding()
    {
        // Arrange
        var bindingExecuted = false;
        var focusedNode = new MockFocusableNode { IsFocused = true };
        focusedNode.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(_ => { bindingExecuted = true; return Task.CompletedTask; }, "Save");
        };
        
        var container = new MockContainerNode();
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();
        var state = new InputRouterState();
        
        var keyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.S);
        
        // Act
        var result = await InputRouter.RouteInputAsync(container, keyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(bindingExecuted);
        Assert.Empty(focusedNode.ReceivedInputs); // OnInput should not be called when binding matches
    }

    [Fact]
    public async Task RouteInput_ChildBindingOverridesParentBinding()
    {
        // Arrange
        var parentBindingExecuted = false;
        var childBindingExecuted = false;
        
        var focusedNode = new MockFocusableNode { IsFocused = true };
        focusedNode.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(_ => { childBindingExecuted = true; return Task.CompletedTask; }, "Child Save");
        };
        
        var container = new MockContainerNode();
        container.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.S).Action(_ => { parentBindingExecuted = true; return Task.CompletedTask; }, "Parent Save");
        };
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();
        var state = new InputRouterState();
        
        var keyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.S);
        
        // Act
        var result = await InputRouter.RouteInputAsync(container, keyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(childBindingExecuted);
        Assert.False(parentBindingExecuted); // Child binding should win
    }

    [Fact]
    public async Task RouteInput_ParentBindingUsedWhenChildHasNone()
    {
        // Arrange
        var parentBindingExecuted = false;
        
        var focusedNode = new MockFocusableNode { IsFocused = true };
        // No bindings on focused node
        
        var container = new MockContainerNode();
        container.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.Q).Action(_ => { parentBindingExecuted = true; return Task.CompletedTask; }, "Quit");
        };
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();
        
        var keyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.Q);
        var state = new InputRouterState();
        
        // Act
        var result = await InputRouter.RouteInputAsync(container, keyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.True(parentBindingExecuted);
    }

    [Fact]
    public async Task RouteInput_NoFocusedNode_ReturnsNotHandled()
    {
        // Arrange
        var unfocusedNode = new MockFocusableNode { IsFocused = false };
        var container = new MockContainerNode();
        container.Children.Add(unfocusedNode);
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        // Don't call EnsureFocus - we want no focus
        
        var keyEvent = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
        var state = new InputRouterState();
        
        // Act
        var result = await InputRouter.RouteInputAsync(container, keyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task RouteInput_NestedContainers_CollectsBindingsFromPath()
    {
        // Arrange - 3 levels: root -> middle -> focused
        var rootBindingExecuted = false;
        var middleBindingExecuted = false;
        
        var focusedNode = new MockFocusableNode { IsFocused = true };
        
        var middleContainer = new MockContainerNode();
        middleContainer.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.M).Action(_ => { middleBindingExecuted = true; return Task.CompletedTask; }, "Middle");
        };
        middleContainer.Children.Add(focusedNode);
        focusedNode.Parent = middleContainer;
        
        var rootContainer = new MockContainerNode();
        rootContainer.BindingsConfig = bindings =>
        {
            bindings.Ctrl().Key(Hex1bKey.R).Action(_ => { rootBindingExecuted = true; return Task.CompletedTask; }, "Root");
        };
        rootContainer.Children.Add(middleContainer);
        middleContainer.Parent = rootContainer;
        
        var focusRing = new FocusRing();
        focusRing.Rebuild(rootContainer);
        focusRing.EnsureFocus();
        var state = new InputRouterState();
        
        // Act - trigger root binding
        var rootKeyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.R);
        var rootResult = await InputRouter.RouteInputAsync(rootContainer, rootKeyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
        // Act - trigger middle binding
        var middleKeyEvent = Hex1bKeyEvent.WithCtrl(Hex1bKey.M);
        var middleResult = await InputRouter.RouteInputAsync(rootContainer, middleKeyEvent, focusRing, state, null, TestContext.Current.CancellationToken);
        
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
        builder.Key(Hex1bKey.A).Action(_ => Task.CompletedTask);
        builder.Ctrl().Key(Hex1bKey.S).Action(_ => Task.CompletedTask);
        builder.Shift().Key(Hex1bKey.Tab).Action(_ => Task.CompletedTask);
        
        var bindings = builder.Bindings;
        
        Assert.Equal(3, bindings.Count);
        
        // Plain binding
        Assert.Equal(Hex1bKey.A, bindings[0].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.None, bindings[0].Steps[0].Modifiers);
        
        // Ctrl binding
        Assert.Equal(Hex1bKey.S, bindings[1].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.Control, bindings[1].Steps[0].Modifiers);
        
        // Shift binding
        Assert.Equal(Hex1bKey.Tab, bindings[2].Steps[0].Key);
        Assert.Equal(Hex1bModifiers.Shift, bindings[2].Steps[0].Modifiers);
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
    public async Task RouteInputToNode_TextBoxBackspace_DeletesAndPositionsCursor()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("hell", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public async Task RouteInputToNode_TextBoxDoubleBackspace_DeletesTwoCharacters()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - two backspaces
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("hel", state.Text);
        Assert.Equal(3, state.CursorPosition);
    }

    #region CharacterBinding Tests

    [Fact]
    public void CharacterBinding_MatchesPrintableText()
    {
        // Arrange
        string received = "";
        var binding = new CharacterBinding(
            text => text.Length > 0 && (text.Length > 1 || !char.IsControl(text[0])), 
            text => received = text, 
            "Insert text");

        // Act & Assert
        Assert.True(binding.Matches("a"));
        Assert.True(binding.Matches("Z"));
        Assert.True(binding.Matches("5"));
        Assert.True(binding.Matches(" "));
        Assert.True(binding.Matches("😀"));  // Emoji as string works!
        Assert.False(binding.Matches("\n"));
        Assert.False(binding.Matches("\t"));
        Assert.False(binding.Matches("\b"));
        Assert.False(binding.Matches(""));
        
        binding.Execute("X");
        Assert.Equal("X", received);
    }

    [Fact]
    public void CharacterBinding_CustomPredicate_MatchesDigitsOnly()
    {
        // Arrange
        string received = "";
        var binding = new CharacterBinding(
            text => text.Length == 1 && char.IsDigit(text[0]), 
            text => received = text, 
            "Insert digit");

        // Act & Assert
        Assert.True(binding.Matches("0"));
        Assert.True(binding.Matches("9"));
        Assert.False(binding.Matches("a"));
        Assert.False(binding.Matches(" "));
        Assert.False(binding.Matches("12"));  // Multiple digits don't match single-digit predicate
        
        binding.Execute("7");
        Assert.Equal("7", received);
    }

    [Fact]
    public void InputBindingsBuilder_AnyCharacter_CreatesCharacterBinding()
    {
        // Arrange
        var builder = new InputBindingsBuilder();
        string received = "";

        // Act
        builder.AnyCharacter().Action(text => received = text, "Type");

        // Assert
        Assert.Single(builder.CharacterBindings);
        Assert.Empty(builder.Bindings);  // Key bindings list should be empty
        
        var binding = builder.CharacterBindings[0];
        Assert.True(binding.Matches("a"));
        Assert.True(binding.Matches("😀"));  // Emoji works
        Assert.False(binding.Matches("\n"));
        Assert.False(binding.Matches(""));
        Assert.Equal("Type", binding.Description);
    }

    [Fact]
    public void InputBindingsBuilder_Character_WithCustomPredicate()
    {
        // Arrange
        var builder = new InputBindingsBuilder();
        string received = "";

        // Act
        builder.Character(text => text.Length == 1 && char.IsLetter(text[0])).Action(text => received = text, "Letters only");

        // Assert
        Assert.Single(builder.CharacterBindings);
        
        var binding = builder.CharacterBindings[0];
        Assert.True(binding.Matches("a"));
        Assert.True(binding.Matches("Z"));
        Assert.False(binding.Matches("5"));
        Assert.False(binding.Matches(" "));
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
    public async Task RouteInputToNode_CharacterBinding_InsertsCharacter()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("helloX", state.Text);
        Assert.Equal(6, state.CursorPosition);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_MultipleCharacters()
    {
        // Arrange
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.H, 'H', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.I, 'i', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.None, '!', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hi!", state.Text);
        Assert.Equal(3, state.CursorPosition);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_NotFocused_NotHandled()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = false };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal("hello", state.Text);  // Text unchanged
    }

    [Fact]
    public async Task RouteInputToNode_KeyBindingTakesPrecedence_OverCharacterBinding()
    {
        // Arrange - node with both a key binding for 'A' and a character binding
        bool keyBindingCalled = false;
        string textReceived = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Key(Hex1bKey.A).Action(_ => { keyBindingCalled = true; return Task.CompletedTask; }, "A key");
            bindings.AnyCharacter().Action(text => textReceived = text, "Type");
        };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert - key binding should win
        Assert.Equal(InputResult.Handled, result);
        Assert.True(keyBindingCalled);
        Assert.Equal("", textReceived);  // Character binding should not have been called
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_FallsBackWhenNoKeyBinding()
    {
        // Arrange - node with key binding for Enter, but typing 'x'
        bool enterPressed = false;
        string textReceived = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Key(Hex1bKey.Enter).Action(_ => { enterPressed = true; return Task.CompletedTask; }, "Submit");
            bindings.AnyCharacter().Action(text => textReceived = text, "Type");
        };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'x', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert - character binding should handle it
        Assert.Equal(InputResult.Handled, result);
        Assert.False(enterPressed);
        Assert.Equal("x", textReceived);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_EmptyText_NotHandled()
    {
        // Arrange - key event with no text
        string textReceived = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.AnyCharacter().Action(text => textReceived = text, "Type");
        };

        // Act - F1 key has no text
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.F1, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert - character binding should not match
        Assert.Equal(InputResult.Handled, result);  // Handled by HandleInput fallback
        Assert.Equal("", textReceived);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_FirstMatchWins()
    {
        // Arrange - multiple character bindings, first matching one wins
        string handler = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Character(text => text.Length == 1 && char.IsDigit(text[0])).Action(_ => handler = "digit", "Digits");
            bindings.AnyCharacter().Action(_ => handler = "any", "Any char");
        };

        // Act - '5' matches both, but digit comes first
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.D5, '5', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("digit", handler);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_SecondMatchWhenFirstFails()
    {
        // Arrange - digit binding first, but typing a letter
        string handler = "";
        
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings =>
        {
            bindings.Character(text => text.Length == 1 && char.IsDigit(text[0])).Action(_ => handler = "digit", "Digits");
            bindings.AnyCharacter().Action(_ => handler = "any", "Any char");
        };

        // Act - 'a' doesn't match digit, falls through to any
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("any", handler);
    }

    [Fact]
    public async Task RouteInput_CharacterBinding_OnlyOnFocusedNode()
    {
        // Arrange - parent has character binding, child is focused
        string parentReceived = "";
        string childReceived = "";
        
        var container = new MockContainerNode();
        container.BindingsConfig = bindings =>
        {
            bindings.AnyCharacter().Action(text => parentReceived = text, "Parent type");
        };
        
        var child = new MockFocusableNode { IsFocused = true };
        child.BindingsConfig = bindings =>
        {
            bindings.AnyCharacter().Action(text => childReceived = text, "Child type");
        };
        
        container.Children.Add(child);
        child.Parent = container;

        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();
        var state = new InputRouterState();

        // Act
        var result = await InputRouter.RouteInputAsync(container, new Hex1bKeyEvent(Hex1bKey.X, 'x', Hex1bModifiers.None), focusRing, state, null, TestContext.Current.CancellationToken);

        // Assert - only child's character binding should fire (not parent's)
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("x", childReceived);
        Assert.Equal("", parentReceived);  // Parent's character binding should NOT have been called
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_Emoji()
    {
        // Arrange - emoji as a full string (not surrogate pair chars)
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - insert an emoji using FromText (simulating paste or input method)
        var result = await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("😀"), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("hello😀", state.Text);
        Assert.Equal(7, state.CursorPosition);  // Emoji is 2 chars in UTF-16
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_MultipleEmojis()
    {
        // Arrange
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - insert multiple emojis
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("👍"), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("🎉"), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("🚀"), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("👍🎉🚀", state.Text);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_UnicodeCharacters()
    {
        // Arrange
        var state = new TextBoxState { Text = "", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - insert various Unicode characters
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.None, 'é', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.None, 'ñ', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.None, '中', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.None, '日', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("éñ中日", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public async Task RouteInputToNode_CharacterBinding_PastedText()
    {
        // Arrange - simulating paste of multiple characters at once
        var state = new TextBoxState { Text = "Hello ", CursorPosition = 6 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - paste "World!" as a single event
        var result = await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("World!"), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("Hello World!", state.Text);
        Assert.Equal(12, state.CursorPosition);
    }

    #endregion
}
