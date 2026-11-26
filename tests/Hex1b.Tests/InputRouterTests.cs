using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class InputRouterTests
{
    [TestMethod]
    public async Task HeldKey_RepeatAndRelease_CancelsOnceWithoutBlockingRouting()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings => bindings.Key(Hex1bKey.A).OnHold(async (_, token) =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult(token);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { finished.TrySetResult(); }
        });
        var container = new MockContainerNode();
        container.Children.Add(node);
        node.Parent = container;
        var ring = new FocusRing();
        ring.Rebuild(container);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');

        await InputRouter.RouteInputAsync(container, press, ring, state);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await InputRouter.RouteInputAsync(container, press with { EventType = Hex1bKeyEventType.Repeat }, ring, state);
        Assert.AreEqual(1, calls);
        Assert.IsFalse(token.IsCancellationRequested);
        await InputRouter.RouteInputAsync(container, press with { EventType = Hex1bKeyEventType.Release, Modifiers = Hex1bModifiers.Shift }, ring, state);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(token.IsCancellationRequested);
        state.CancelKeyHolds();
    }

    [TestMethod]
    public async Task HeldKey_CompletedCallback_TokenStillCancelsOnRelease()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings => bindings.Key(Hex1bKey.A).OnHold((_, token) => started.TrySetResult(token));
        var container = new MockContainerNode();
        container.Children.Add(node);
        node.Parent = container;
        var ring = new FocusRing();
        ring.Rebuild(container);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
        await InputRouter.RouteInputAsync(container, press, ring, state);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await InputRouter.RouteInputAsync(container, press with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.IsTrue(token.IsCancellationRequested);
        state.CancelKeyHolds();
    }

    [TestMethod]
    public async Task HeldKey_FocusCaptureAndShutdown_CancelTokens()
    {
        var started = new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true };
        var tokens = System.Threading.Channels.Channel.CreateUnbounded<CancellationToken>(started);
        var first = new MockFocusableNode { IsFocused = true };
        var second = new MockFocusableNode();
        first.BindingsConfig = second.BindingsConfig = bindings =>
            bindings.Key(Hex1bKey.A).OnHold((_, token) => tokens.Writer.TryWrite(token));
        var container = new MockContainerNode();
        container.Children.AddRange([first, second]);
        first.Parent = second.Parent = container;
        var ring = new FocusRing();
        ring.Rebuild(container);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');

        await InputRouter.RouteInputAsync(container, press, ring, state);
        var firstToken = await tokens.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        ring.Focus(second);
        Assert.IsTrue(firstToken.IsCancellationRequested);
        await InputRouter.RouteInputAsync(container, press, ring, state);
        var secondToken = await tokens.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        ring.CaptureInput(second);
        Assert.IsTrue(secondToken.IsCancellationRequested);
        ring.ReleaseCapture();
        await InputRouter.RouteInputAsync(container, press, ring, state);
        var thirdToken = await tokens.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        state.CancelKeyHolds();
        Assert.IsTrue(thirdToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task HeldKey_FailureAfterShutdown_DoesNotFailNextRun()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var node = new MockFocusableNode { IsFocused = true };
        node.BindingsConfig = bindings => bindings.Key(Hex1bKey.A).OnHold(async (_, _) =>
        {
            started.TrySetResult();
            await finish.Task;
            throw new InvalidOperationException("old run");
        });
        var container = new MockContainerNode();
        container.Children.Add(node);
        node.Parent = container;
        var ring = new FocusRing();
        ring.Rebuild(container);
        var state = new InputRouterState();
        state.BeginKeyHolds();
        var oldFailure = state.HoldFailure;

        await InputRouter.RouteInputAsync(container, Hex1bKeyEvent.Plain(Hex1bKey.A), ring, state);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        state.CancelKeyHolds();
        state.BeginKeyHolds();
        finish.TrySetResult();

        Assert.AreEqual("old run", (await oldFailure.WaitAsync(TimeSpan.FromSeconds(5))).Message);
        Assert.IsFalse(state.HoldFailure.IsCompleted);
    }

    [TestMethod]
    public async Task RouteInput_EnhancedModifiersAndLocks_MatchesBinding()
    {
        var actions = 0;
        var focusedNode = new MockFocusableNode { IsFocused = true };
        focusedNode.BindingsConfig = bindings =>
            bindings.Super().Ctrl().Key(Hex1bKey.A).Action(() => actions++);
        var container = new MockContainerNode();
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();

        await InputRouter.RouteInputAsync(container,
            new Hex1bKeyEvent(Hex1bKey.A, "", Hex1bModifiers.Super | Hex1bModifiers.Control |
                Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock),
            focusRing, new InputRouterState());
        Assert.AreEqual(1, actions);
    }

    [TestMethod]
    public async Task RouteInput_Release_DoesNotTriggerBindingsOrCancelChord()
    {
        var actions = 0;
        var focusedNode = new MockFocusableNode { IsFocused = true };
        focusedNode.BindingsConfig = bindings =>
            bindings.Key(Hex1bKey.G).Then().Key(Hex1bKey.G)
                .Action(_ => { actions++; return Task.CompletedTask; }, "Go");
        var container = new MockContainerNode();
        container.Children.Add(focusedNode);
        focusedNode.Parent = container;
        var focusRing = new FocusRing();
        focusRing.Rebuild(container);
        focusRing.EnsureFocus();
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.G, 'g');
        var release = press with { EventType = Hex1bKeyEventType.Release };

        await InputRouter.RouteInputAsync(container, press, focusRing, state);
        await InputRouter.RouteInputAsync(container, release, focusRing, state);
        Assert.AreEqual(release, TestSeq.Single(focusedNode.ReceivedInputs));
        Assert.AreEqual(0, actions);
        await InputRouter.RouteInputAsync(container, press, focusRing, state);
        Assert.AreEqual(1, actions);
    }

    /// <summary>
    /// Mock focusable node for testing input routing.
    /// </summary>
    private sealed class MockFocusableNode : Hex1bNode
    {
        public override bool IsFocusable => true;
        private bool _isFocused;
        public override bool IsFocused { get => _isFocused; set => _isFocused = value; }
        
        public List<Hex1bEvent> ReceivedInputs { get; } = new();
        
        // Action to configure bindings for this node
        public Action<InputBindingsBuilder>? BindingsConfig { get; set; }
        
        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
        {
            BindingsConfig?.Invoke(bindings);
        }
        
        protected override Size MeasureCore(Layout.Constraints constraints) => new Size(10, 1);
        public override void Render(Hex1bRenderContext context) { }
        
        public override InputResult HandleInput(Hex1bEvent inputEvent)
        {
            ReceivedInputs.Add(inputEvent);
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
        
        protected override Size MeasureCore(Layout.Constraints constraints) => new Size(80, 24);
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

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        TestSeq.Single(focusedNode.ReceivedInputs);
        var receivedKeyEvent = TestSeq.IsType<Hex1bKeyEvent>(focusedNode.ReceivedInputs[0]);
        Assert.AreEqual(Hex1bKey.A, receivedKeyEvent.Key);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(bindingExecuted);
        Assert.IsEmpty(focusedNode.ReceivedInputs); // OnInput should not be called when binding matches
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(childBindingExecuted);
        Assert.IsFalse(parentBindingExecuted); // Child binding should win
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(parentBindingExecuted);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.NotHandled, result);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, rootResult);
        Assert.IsTrue(rootBindingExecuted);
        Assert.AreEqual(InputResult.Handled, middleResult);
        Assert.IsTrue(middleBindingExecuted);
    }

    [TestMethod]
    public void InputBinding_Factory_CreatesCorrectBindings()
    {
        // Test InputBindingsBuilder creates correct key/modifier combinations
        var builder = new InputBindingsBuilder();
        builder.Key(Hex1bKey.A).Action(_ => Task.CompletedTask);
        builder.Ctrl().Key(Hex1bKey.S).Action(_ => Task.CompletedTask);
        builder.Shift().Key(Hex1bKey.Tab).Action(_ => Task.CompletedTask);
        
        var bindings = builder.Bindings;
        
        Assert.AreEqual(3, bindings.Count);
        
        // Plain binding
        Assert.AreEqual(Hex1bKey.A, bindings[0].Steps[0].Key);
        Assert.AreEqual(Hex1bModifiers.None, bindings[0].Steps[0].Modifiers);
        
        // Ctrl binding
        Assert.AreEqual(Hex1bKey.S, bindings[1].Steps[0].Key);
        Assert.AreEqual(Hex1bModifiers.Control, bindings[1].Steps[0].Modifiers);
        
        // Shift binding
        Assert.AreEqual(Hex1bKey.Tab, bindings[2].Steps[0].Key);
        Assert.AreEqual(Hex1bModifiers.Shift, bindings[2].Steps[0].Modifiers);
    }

    [TestMethod]
    public void Hex1bKeyEvent_Properties_ReturnCorrectValues()
    {
        var evt = new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control | Hex1bModifiers.Shift);
        
        Assert.IsTrue(evt.Control);
        Assert.IsTrue(evt.Shift);
        Assert.IsFalse(evt.Alt);
        Assert.IsTrue(evt.IsPrintable);
        
        var nonPrintable = new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None);
        Assert.IsFalse(nonPrintable.IsPrintable);
    }

    [TestMethod]
    public void KeyMapper_ToHex1bKey_MapsCorrectly()
    {
        Assert.AreEqual(Hex1bKey.A, KeyMapper.ToHex1bKey(ConsoleKey.A));
        Assert.AreEqual(Hex1bKey.Enter, KeyMapper.ToHex1bKey(ConsoleKey.Enter));
        Assert.AreEqual(Hex1bKey.Escape, KeyMapper.ToHex1bKey(ConsoleKey.Escape));
        Assert.AreEqual(Hex1bKey.Tab, KeyMapper.ToHex1bKey(ConsoleKey.Tab));
        Assert.AreEqual(Hex1bKey.UpArrow, KeyMapper.ToHex1bKey(ConsoleKey.UpArrow));
        Assert.AreEqual(Hex1bKey.F1, KeyMapper.ToHex1bKey(ConsoleKey.F1));
    }

    [TestMethod]
    public void KeyMapper_ToHex1bModifiers_MapsCorrectly()
    {
        Assert.AreEqual(Hex1bModifiers.None, KeyMapper.ToHex1bModifiers(false, false, false));
        Assert.AreEqual(Hex1bModifiers.Shift, KeyMapper.ToHex1bModifiers(true, false, false));
        Assert.AreEqual(Hex1bModifiers.Alt, KeyMapper.ToHex1bModifiers(false, true, false));
        Assert.AreEqual(Hex1bModifiers.Control, KeyMapper.ToHex1bModifiers(false, false, true));
        Assert.AreEqual(Hex1bModifiers.Shift | Hex1bModifiers.Control, KeyMapper.ToHex1bModifiers(true, false, true));
    }

    [TestMethod]
    public async Task RouteInputToNode_TextBoxBackspace_DeletesAndPositionsCursor()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("hell", state.Text);
        Assert.AreEqual(4, state.CursorPosition);
    }

    [TestMethod]
    public async Task RouteInputToNode_TextBoxDoubleBackspace_DeletesTwoCharacters()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - two backspaces
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.AreEqual("hel", state.Text);
        Assert.AreEqual(3, state.CursorPosition);
    }

    #region CharacterBinding Tests

    [TestMethod]
    public void CharacterBinding_MatchesPrintableText()
    {
        // Arrange
        string received = "";
        var binding = new CharacterBinding(
            text => text.Length > 0 && (text.Length > 1 || !char.IsControl(text[0])), 
            text => received = text, 
            "Insert text");

        // Act & Assert
        Assert.IsTrue(binding.Matches("a"));
        Assert.IsTrue(binding.Matches("Z"));
        Assert.IsTrue(binding.Matches("5"));
        Assert.IsTrue(binding.Matches(" "));
        Assert.IsTrue(binding.Matches("😀"));  // Emoji as string works!
        Assert.IsFalse(binding.Matches("\n"));
        Assert.IsFalse(binding.Matches("\t"));
        Assert.IsFalse(binding.Matches("\b"));
        Assert.IsFalse(binding.Matches(""));
        
        binding.Execute("X");
        Assert.AreEqual("X", received);
    }

    [TestMethod]
    public void CharacterBinding_CustomPredicate_MatchesDigitsOnly()
    {
        // Arrange
        string received = "";
        var binding = new CharacterBinding(
            text => text.Length == 1 && char.IsDigit(text[0]), 
            text => received = text, 
            "Insert digit");

        // Act & Assert
        Assert.IsTrue(binding.Matches("0"));
        Assert.IsTrue(binding.Matches("9"));
        Assert.IsFalse(binding.Matches("a"));
        Assert.IsFalse(binding.Matches(" "));
        Assert.IsFalse(binding.Matches("12"));  // Multiple digits don't match single-digit predicate
        
        binding.Execute("7");
        Assert.AreEqual("7", received);
    }

    [TestMethod]
    public void InputBindingsBuilder_AnyCharacter_CreatesCharacterBinding()
    {
        // Arrange
        var builder = new InputBindingsBuilder();
        string received = "";

        // Act
        builder.AnyCharacter().Action(text => received = text, "Type");

        // Assert
        TestSeq.Single(builder.CharacterBindings);
        Assert.IsEmpty(builder.Bindings);  // Key bindings list should be empty
        
        var binding = builder.CharacterBindings[0];
        Assert.IsTrue(binding.Matches("a"));
        Assert.IsTrue(binding.Matches("😀"));  // Emoji works
        Assert.IsFalse(binding.Matches("\n"));
        Assert.IsFalse(binding.Matches(""));
        Assert.AreEqual("Type", binding.Description);
    }

    [TestMethod]
    public void InputBindingsBuilder_Character_WithCustomPredicate()
    {
        // Arrange
        var builder = new InputBindingsBuilder();
        string received = "";

        // Act
        builder.Character(text => text.Length == 1 && char.IsLetter(text[0])).Action(text => received = text, "Letters only");

        // Assert
        TestSeq.Single(builder.CharacterBindings);
        
        var binding = builder.CharacterBindings[0];
        Assert.IsTrue(binding.Matches("a"));
        Assert.IsTrue(binding.Matches("Z"));
        Assert.IsFalse(binding.Matches("5"));
        Assert.IsFalse(binding.Matches(" "));
    }

    [TestMethod]
    public void InputBindingsBuilder_MixedBindings_KeyAndCharacter()
    {
        // Arrange
        var builder = new InputBindingsBuilder();

        // Act
        builder.Key(Hex1bKey.Enter).Action(() => { }, "Submit");
        builder.AnyCharacter().Action(_ => { }, "Type");

        // Assert
        TestSeq.Single(builder.Bindings);
        TestSeq.Single(builder.CharacterBindings);
    }

    [TestMethod]
    public async Task RouteInputToNode_CharacterBinding_InsertsCharacter()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("helloX", state.Text);
        Assert.AreEqual(6, state.CursorPosition);
    }

    [TestMethod]
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
        Assert.AreEqual("Hi!", state.Text);
        Assert.AreEqual(3, state.CursorPosition);
    }

    [TestMethod]
    public async Task RouteInputToNode_CharacterBinding_NotFocused_NotHandled()
    {
        // Arrange
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = false };

        // Act
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.AreEqual(InputResult.NotHandled, result);
        Assert.AreEqual("hello", state.Text);  // Text unchanged
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(keyBindingCalled);
        Assert.AreEqual("", textReceived);  // Character binding should not have been called
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsFalse(enterPressed);
        Assert.AreEqual("x", textReceived);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);  // Handled by HandleInput fallback
        Assert.AreEqual("", textReceived);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("digit", handler);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("any", handler);
    }

    [TestMethod]
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
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("x", childReceived);
        Assert.AreEqual("", parentReceived);  // Parent's character binding should NOT have been called
    }

    [TestMethod]
    public async Task RouteInputToNode_CharacterBinding_Emoji()
    {
        // Arrange - emoji as a full string (not surrogate pair chars)
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - insert an emoji using FromText (simulating paste or input method)
        var result = await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("😀"), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("hello😀", state.Text);
        Assert.AreEqual(7, state.CursorPosition);  // Emoji is 2 chars in UTF-16
    }

    [TestMethod]
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
        Assert.AreEqual("👍🎉🚀", state.Text);
    }

    [TestMethod]
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
        Assert.AreEqual("éñ中日", state.Text);
        Assert.AreEqual(4, state.CursorPosition);
    }

    [TestMethod]
    public async Task RouteInputToNode_CharacterBinding_PastedText()
    {
        // Arrange - simulating paste of multiple characters at once
        var state = new TextBoxState { Text = "Hello ", CursorPosition = 6 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        // Act - paste "World!" as a single event
        var result = await InputRouter.RouteInputToNodeAsync(node, Hex1bKeyEvent.FromText("World!"), null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual("Hello World!", state.Text);
        Assert.AreEqual(12, state.CursorPosition);
    }

    #endregion
}
