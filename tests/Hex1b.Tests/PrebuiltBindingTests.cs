using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the public <c>InputBindingsBuilder.Add(...)</c> overloads that allow app code
/// to register prebuilt <see cref="InputBinding"/>, <see cref="MouseBinding"/>,
/// <see cref="CharacterBinding"/>, and <see cref="DragBinding"/> instances. Covers issue
/// <see href="https://github.com/mitchdenny/hex1b/issues/292">#292</see>.
/// </summary>
public class PrebuiltBindingTests
{
    /// <summary>
    /// Mock focusable node used to drive end-to-end character/key routing without
    /// pulling in real widgets. Mirrors the pattern in <see cref="InputRouterTests"/>.
    /// </summary>
    private sealed class MockFocusableNode : Hex1bNode
    {
        public override bool IsFocusable => true;
        private bool _isFocused;
        public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

        public Action<InputBindingsBuilder>? BindingsConfig { get; set; }

        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
        {
            BindingsConfig?.Invoke(bindings);
        }

        protected override Size MeasureCore(Constraints constraints) => new(10, 1);
        public override void Render(Hex1bRenderContext context) { }
    }

    #region 1. Null guards

    [Fact]
    public void Add_NullInputBinding_ThrowsArgumentNullException()
    {
        var builder = new InputBindingsBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Add((InputBinding)null!));
    }

    [Fact]
    public void Add_NullMouseBinding_ThrowsArgumentNullException()
    {
        var builder = new InputBindingsBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Add((MouseBinding)null!));
    }

    [Fact]
    public void Add_NullCharacterBinding_ThrowsArgumentNullException()
    {
        var builder = new InputBindingsBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Add((CharacterBinding)null!));
    }

    [Fact]
    public void Add_NullDragBinding_ThrowsArgumentNullException()
    {
        var builder = new InputBindingsBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Add((DragBinding)null!));
    }

    #endregion

    #region 2. Round-trip via the public Bindings/MouseBindings/etc. lists

    [Fact]
    public void Add_PrebuiltInputBinding_AppearsInBindingsList()
    {
        var builder = new InputBindingsBuilder();
        var binding = new InputBinding(
            [new KeyStep(Hex1bKey.LeftArrow, Hex1bModifiers.Control | Hex1bModifiers.Shift)],
            () => { },
            "Select word left");

        builder.Add(binding);

        var actual = Assert.Single(builder.Bindings);
        Assert.Same(binding, actual);
        Assert.Empty(builder.MouseBindings);
        Assert.Empty(builder.CharacterBindings);
        Assert.Empty(builder.DragBindings);
    }

    [Fact]
    public void Add_PrebuiltMouseBinding_AppearsInMouseBindingsList()
    {
        var builder = new InputBindingsBuilder();
        var binding = new MouseBinding(MouseButton.Left, MouseAction.Down, Hex1bModifiers.None, () => { }, "click");

        builder.Add(binding);

        var actual = Assert.Single(builder.MouseBindings);
        Assert.Same(binding, actual);
    }

    [Fact]
    public void Add_PrebuiltCharacterBinding_AppearsInCharacterBindingsList()
    {
        var builder = new InputBindingsBuilder();
        var binding = new CharacterBinding(text => text.Length == 1, _ => { }, "any single char");

        builder.Add(binding);

        var actual = Assert.Single(builder.CharacterBindings);
        Assert.Same(binding, actual);
    }

    [Fact]
    public void Add_PrebuiltDragBinding_AppearsInDragBindingsList()
    {
        var builder = new InputBindingsBuilder();
        var binding = new DragBinding(
            MouseButton.Left,
            Hex1bModifiers.None,
            (_, _) => new DragHandler(),
            "drag");

        builder.Add(binding);

        var actual = Assert.Single(builder.DragBindings);
        Assert.Same(binding, actual);
    }

    #endregion

    #region 3. End-to-end routing through Add(prebuilt)

    [Fact]
    public async Task Add_PrebuiltCtrlShiftInputBinding_FiresEndToEnd()
    {
        // RULE: A Ctrl+Shift+Key binding constructed externally and registered via
        //       InputBindingsBuilder.Add(InputBinding) routes correctly when the
        //       terminal delivers a Ctrl+Shift+Key event. Mirrors the fluent-path
        //       coverage in InputBindingPrecedenceTests.CtrlShiftBinding_GlobalBinding_FiresEndToEnd.
        // SETUP: VStack with a prebuilt Ctrl+Shift+LeftArrow binding added via b.Add(...).
        // ACTION: Press Ctrl+Shift+LeftArrow.
        // EXPECTED: Binding fires.

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var bindingFired = false;
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var prebuilt = new InputBinding(
            [new KeyStep(Hex1bKey.LeftArrow, Hex1bModifiers.Control | Hex1bModifiers.Shift)],
            (Action<InputBindingActionContext>)(_ => { bindingFired = true; }),
            "Prebuilt Ctrl+Shift+LeftArrow");

        using var app = new Hex1bApp(
            ctx => ctx.VStack(v => [
                v.Test().OnRender(_ => renderOccurred.TrySetResult())
            ]).WithInputBindings(b => b.Add(prebuilt)),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Shift().Key(Hex1bKey.LeftArrow).Wait(100).Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(bindingFired, "Prebuilt Ctrl+Shift+LeftArrow binding should fire end-to-end");

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region 4. Constructor parameter flow-through

    [Fact]
    public void InputBinding_ConstructedWithActionId_ExposesActionId()
    {
        var actionId = new ActionId("Test.Foo");
        var binding = new InputBinding(
            [new KeyStep(Hex1bKey.A)],
            () => { },
            description: "foo",
            isGlobal: false,
            actionId: actionId);

        Assert.Equal(actionId, binding.ActionId);
        Assert.False(binding.OverridesCapture);
    }

    [Fact]
    public void InputBinding_ConstructedWithOverridesCapture_ExposesFlag()
    {
        var binding = new InputBinding(
            [new KeyStep(Hex1bKey.A)],
            () => { },
            description: "foo",
            isGlobal: false,
            actionId: null,
            overridesCapture: true);

        Assert.True(binding.OverridesCapture);
        Assert.Null(binding.ActionId);
    }

    [Fact]
    public void MouseBinding_ConstructedWithActionId_ExposesActionId()
    {
        var actionId = new ActionId("Test.Click");
        var binding = new MouseBinding(
            MouseButton.Left,
            MouseAction.Down,
            Hex1bModifiers.None,
            () => { },
            "click",
            actionId);

        Assert.Equal(actionId, binding.ActionId);
    }

    [Fact]
    public void CharacterBinding_ConstructedWithActionId_ExposesActionId()
    {
        var actionId = new ActionId("Test.Type");
        var binding = new CharacterBinding(
            _ => true,
            _ => { },
            "type",
            actionId);

        Assert.Equal(actionId, binding.ActionId);
    }

    [Fact]
    public void DragBinding_ConstructedWithActionId_ExposesActionId()
    {
        var actionId = new ActionId("Test.Drag");
        var binding = new DragBinding(
            MouseButton.Left,
            Hex1bModifiers.None,
            (_, _) => new DragHandler(),
            "drag",
            actionId);

        Assert.Equal(actionId, binding.ActionId);
    }

    #endregion

    #region 5. ActionId enables Remove(ActionId) for prebuilt bindings

    [Fact]
    public void Remove_ByActionId_RemovesPrebuiltMouseBinding()
    {
        var builder = new InputBindingsBuilder();
        var actionId = new ActionId("Test.Click");
        var binding = new MouseBinding(MouseButton.Left, MouseAction.Down, Hex1bModifiers.None, () => { }, "click", actionId);

        builder.Add(binding);
        Assert.Single(builder.MouseBindings);

        builder.Remove(actionId);

        Assert.Empty(builder.MouseBindings);
    }

    [Fact]
    public void Remove_ByActionId_RemovesPrebuiltInputBinding()
    {
        var builder = new InputBindingsBuilder();
        var actionId = new ActionId("Test.Foo");
        var binding = new InputBinding([new KeyStep(Hex1bKey.A)], () => { }, "foo", false, actionId);

        builder.Add(binding);
        Assert.Single(builder.Bindings);

        builder.Remove(actionId);

        Assert.Empty(builder.Bindings);
    }

    #endregion

    #region 6. End-to-end OverridesCapture flag honored

    [Fact]
    public async Task Add_PrebuiltOverridesCaptureBinding_FiresWhenCaptured()
    {
        // RULE: A prebuilt InputBinding constructed with overridesCapture: true must
        //       fire even when a downstream node (TerminalWidget) has captured input.
        //       Mirrors the fluent-path coverage in CaptureOverrideChordTests.
        // SETUP: VStack containing a child terminal that captures input + a prebuilt
        //        Ctrl+B binding registered via b.Add(...) with overridesCapture: true.
        // ACTION: Send Ctrl+B while the terminal has capture.
        // EXPECTED: The prebuilt binding fires.

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        var bindingFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var childWorkload = StreamWorkloadAdapter.CreateHeadless(40, 10);
        using var childTerminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithHeadless().WithDimensions(40, 10)
            .WithTerminalWidget(out var handle).Build();

        using var childCts = new CancellationTokenSource();
        _ = childTerminal.RunAsync(childCts.Token);

        var prebuilt = new InputBinding(
            [new KeyStep(Hex1bKey.B, Hex1bModifiers.Control)],
            (Action<InputBindingActionContext>)(_ => bindingFired.TrySetResult()),
            description: "Prebuilt override capture",
            isGlobal: false,
            actionId: null,
            overridesCapture: true);

        using var app = new Hex1bApp(
            ctx =>
            {
                var widget = ctx.VStack(v => [
                    v.Terminal(handle).Fill(),
                    v.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(b => b.Add(prebuilt));

                return Task.FromResult<Hex1bWidget>(widget);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.B).Wait(100)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await bindingFired.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        await childCts.CancelAsync();
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    #endregion

    #region 7. End-to-end character binding via Add(prebuilt)

    [Fact]
    public async Task Add_PrebuiltCharacterBinding_FiresOnTextInput()
    {
        // RULE: A CharacterBinding constructed externally and registered via
        //       InputBindingsBuilder.Add(CharacterBinding) fires when its predicate
        //       matches an incoming text input event. Character bindings only route
        //       to the focused node, so the receiver must be focusable+focused.
        // SETUP: A focusable mock node with a prebuilt AnyCharacter-style binding.
        // ACTION: Route a Hex1bKeyEvent carrying the character 'x'.
        // EXPECTED: The handler receives "x".

        var received = "";
        var node = new MockFocusableNode
        {
            IsFocused = true,
            BindingsConfig = b => b.Add(new CharacterBinding(
                text => text.Length > 0 && !char.IsControl(text[0]),
                text => received = text,
                "Prebuilt any character"))
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.X, 'x', Hex1bModifiers.None),
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("x", received);
    }

    #endregion

    #region 8. End-to-end drag binding via Add(prebuilt)

    [Fact]
    public void Add_PrebuiltDragBinding_FactoryInvokedByStartDrag()
    {
        // RULE: A DragBinding constructed externally and registered via
        //       InputBindingsBuilder.Add(DragBinding) participates in drag dispatch
        //       just like a fluent-built one. We exercise the binding's StartDrag
        //       contract directly because end-to-end mouse drag wiring is covered
        //       elsewhere; this test confirms the prebuilt instance round-trips.
        // SETUP: A drag binding that records the (x, y) it was started at.
        // ACTION: Add it, look it up, and invoke StartDrag(7, 11).
        // EXPECTED: The factory was invoked with (7, 11) and the binding matches a
        //           Mouse Down event for the configured button + modifiers.

        (int x, int y)? startedAt = null;
        var binding = new DragBinding(
            MouseButton.Left,
            Hex1bModifiers.None,
            (x, y) =>
            {
                startedAt = (x, y);
                return new DragHandler();
            },
            "drag");

        var builder = new InputBindingsBuilder();
        builder.Add(binding);

        var added = Assert.Single(builder.DragBindings);
        Assert.True(added.Matches(new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None, ClickCount: 1)));
        added.StartDrag(7, 11);

        Assert.Equal((7, 11), startedAt);
    }

    #endregion

    #region 9. Conflict detection still applies to prebuilt globals

    [Fact]
    public async Task Add_PrebuiltGlobalBinding_ConflictingDefaultGlobal_Throws()
    {
        // RULE: When two global bindings claim the same first key step, the input
        //       router's CollectGlobalBindings must throw an InvalidOperationException.
        //       The new public Add(InputBinding) path must not bypass this safety net.
        // SETUP: Two non-focusable VStacks, one nested in the other. Outer registers a
        //        global binding for 'X' via the fluent .Global() path; inner registers
        //        another global binding for 'X' via Add(prebuilt with isGlobal: true).
        // ACTION: Press 'X' to trigger global collection during input routing.
        // EXPECTED: app.RunAsync surfaces an InvalidOperationException about a global
        //           binding conflict.

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var renderOccurred = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var prebuiltGlobal = new InputBinding(
            [new KeyStep(Hex1bKey.X)],
            () => { },
            description: "Prebuilt global X",
            isGlobal: true);

        using var app = new Hex1bApp(
            ctx => ctx.VStack(outer => [
                outer.VStack(inner => [
                    inner.Test().OnRender(_ => renderOccurred.TrySetResult())
                ]).WithInputBindings(b => b.Add(prebuiltGlobal))
            ]).WithInputBindings(b =>
            {
                b.Key(Hex1bKey.X).Global().Action(() => { }, "Fluent global X");
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await renderOccurred.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder().Key(Hex1bKey.X).Wait(100).Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await runTask);
        Assert.Contains("Global binding conflict", ex.Message);

        cts.Cancel();
    }

    #endregion

    #region 10. Defensive copy of Steps

    [Fact]
    public void InputBinding_StepsListMutatedAfterConstruction_DoesNotAffectBinding()
    {
        // RULE: InputBinding must defensively copy its steps list so that callers
        //       mutating the source list after construction can't change runtime
        //       behavior of an already-registered binding.

        var steps = new List<KeyStep>
        {
            new(Hex1bKey.LeftArrow, Hex1bModifiers.Control | Hex1bModifiers.Shift)
        };

        var binding = new InputBinding(steps, () => { }, "select word left");

        steps.Clear();
        steps.Add(new KeyStep(Hex1bKey.RightArrow, Hex1bModifiers.None));

        var step = Assert.Single(binding.Steps);
        Assert.Equal(Hex1bKey.LeftArrow, step.Key);
        Assert.Equal(Hex1bModifiers.Control | Hex1bModifiers.Shift, step.Modifiers);
    }

    #endregion

    #region 11. ActionId via ctor does NOT register for Triggers(actionId) rebinding

    [Fact]
    public void Triggers_ByActionId_AfterAddPrebuiltOnly_ThrowsInvalidOperation()
    {
        // RULE: An ActionId supplied via the InputBinding ctor identifies the binding
        //       for Remove(ActionId) and GetBindings(ActionId) only — it does NOT
        //       populate the rebinding registry. Apps that need Triggers(actionId)
        //       rebinding support must use the fluent Triggers(actionId, handler, ...)
        //       path, which both registers the binding and seeds the registry.
        //       This test locks in that documented limitation so a future implementation
        //       change is a deliberate decision rather than an accident.

        var builder = new InputBindingsBuilder();
        var actionId = new ActionId("Test.Foo");
        var prebuilt = new InputBinding(
            [new KeyStep(Hex1bKey.A)],
            () => { },
            description: "foo",
            isGlobal: false,
            actionId: actionId);

        builder.Add(prebuilt);

        // Sanity check: the binding is queryable by its ActionId for inspection/removal.
        Assert.Single(builder.GetBindings(actionId));

        // But the rebinding registry was NOT seeded, so Triggers(actionId) fails predictably.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Key(Hex1bKey.B).Triggers(actionId));
        Assert.Contains(actionId.Value, ex.Message);
        Assert.Contains("has not been registered", ex.Message);
    }

    #endregion
}
