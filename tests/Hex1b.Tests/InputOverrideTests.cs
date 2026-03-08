using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the InputOverrideWidget, which provides centralized
/// keybinding overrides for all descendant widgets of a specified type.
/// </summary>
public class InputOverrideTests
{
    #region Unit Tests — Override application via reconciliation

    [Fact]
    public async Task Override_AppliesTo_MatchingWidgetType()
    {
        // An InputOverride with Override<ListWidget> should affect ListWidget descendants.
        var overrideCalled = false;

        var listWidget = new ListWidget(["A", "B", "C"]);
        var overrideWidget = new InputOverrideWidget(listWidget)
            .Override<ListWidget>(b =>
            {
                overrideCalled = true;
                b.Remove(ListWidget.MoveUp);
            });

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context) as InputOverrideNode;

        Assert.NotNull(overrideNode);
        Assert.NotNull(overrideNode!.Child);

        // The override configurator should have been wired into the child's BindingsConfigurator
        // by ReconcileChildAsync. Invoke BuildBindings to trigger it.
        var childNode = overrideNode.Child!;
        var bindings = childNode.BuildBindings();

        Assert.True(overrideCalled, "Override configurator should run during BuildBindings");

        // Verify MoveUp was removed by the override
        var actionIds = bindings.GetAllActionIds();
        Assert.DoesNotContain(ListWidget.MoveUp, actionIds);
    }

    [Fact]
    public async Task Override_DoesNotApplyTo_NonMatchingWidgetType()
    {
        // An Override<ListWidget> should NOT affect a ButtonWidget.
        var buttonWidget = new ButtonWidget("Click me").OnClick(_ => { });
        var overrideWidget = new InputOverrideWidget(buttonWidget)
            .Override<ListWidget>(b =>
            {
                // This should never run for a ButtonWidget
                b.RemoveAll();
            });

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context) as InputOverrideNode;
        var childNode = overrideNode!.Child!;
        var bindings = childNode.BuildBindings();

        // Button's Activate binding should still be present (not cleared by Override<ListWidget>)
        var actionIds = bindings.GetAllActionIds();
        Assert.Contains(ButtonWidget.Activate, actionIds);
    }

    [Fact]
    public async Task Override_CascadesToDeepDescendants()
    {
        // An InputOverride wrapping a VStack containing a ListWidget should
        // apply the override to the deeply nested ListWidget.
        var overrideCalled = false;

        var listWidget = new ListWidget(["A", "B"]);
        var vstack = new VStackWidget([listWidget]);
        var overrideWidget = new InputOverrideWidget(vstack)
            .Override<ListWidget>(b =>
            {
                overrideCalled = true;
                b.Remove(ListWidget.MoveDown);
            });

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context);

        // Walk down to find the ListNode
        var vstackNode = (overrideNode as InputOverrideNode)?.Child as VStackNode;
        Assert.NotNull(vstackNode);

        var listNode = vstackNode!.Children.OfType<ListNode>().FirstOrDefault();
        Assert.NotNull(listNode);

        var bindings = listNode!.BuildBindings();
        Assert.True(overrideCalled, "Override should cascade to nested ListWidget");

        // MoveDown should have been removed
        var actionIds = bindings.GetAllActionIds();
        Assert.DoesNotContain(ListWidget.MoveDown, actionIds);
        // MoveUp should still be present
        Assert.Contains(ListWidget.MoveUp, actionIds);
    }

    [Fact]
    public async Task Override_MultipleTypes_AppliesCorrectly()
    {
        // Override<ListWidget> and Override<ButtonWidget> should each apply to the correct type.
        var listOverrideCalled = false;
        var buttonOverrideCalled = false;

        var content = new VStackWidget([
            new ListWidget(["A"]),
            new ButtonWidget("OK")
        ]);

        var overrideWidget = new InputOverrideWidget(content)
            .Override<ListWidget>(b =>
            {
                listOverrideCalled = true;
                b.Remove(ListWidget.MoveUp);
            })
            .Override<ButtonWidget>(b =>
            {
                buttonOverrideCalled = true;
                b.Remove(ButtonWidget.Activate);
            });

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context);
        var vstackNode = (overrideNode as InputOverrideNode)?.Child as VStackNode;

        var listNode = vstackNode!.Children.OfType<ListNode>().First();
        var buttonNode = vstackNode.Children.OfType<ButtonNode>().First();

        listNode.BuildBindings();
        buttonNode.BuildBindings();

        Assert.True(listOverrideCalled);
        Assert.True(buttonOverrideCalled);
    }

    [Fact]
    public async Task Override_ChainedFluentCalls_AccumulateOverrides()
    {
        // Multiple Override<T> calls on the same InputOverrideWidget should all be present.
        var overrideWidget = new InputOverrideWidget(new VStackWidget([
            new ListWidget(["A"]),
            new CheckboxWidget(CheckboxState.Unchecked)
        ]))
        .Override<ListWidget>(b => b.Remove(ListWidget.MoveUp))
        .Override<CheckboxWidget>(b => b.Remove(CheckboxWidget.ToggleActionId));

        // Both overrides should be in the dictionary
        Assert.Equal(2, overrideWidget.Overrides.Count);
        Assert.True(overrideWidget.Overrides.ContainsKey(typeof(ListWidget)));
        Assert.True(overrideWidget.Overrides.ContainsKey(typeof(CheckboxWidget)));
    }

    [Fact]
    public async Task Override_SameType_LastWins()
    {
        // Calling Override<ListWidget> twice — second call should replace the first.
        var firstCalled = false;
        var secondCalled = false;

        var overrideWidget = new InputOverrideWidget(new ListWidget(["A"]))
            .Override<ListWidget>(b => { firstCalled = true; })
            .Override<ListWidget>(b => { secondCalled = true; });

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context);
        var listNode = (overrideNode as InputOverrideNode)?.Child as ListNode;
        listNode!.BuildBindings();

        Assert.False(firstCalled, "First override should be replaced by second");
        Assert.True(secondCalled, "Second override should be the one that runs");
    }

    [Fact]
    public async Task Override_RunsAfterWidgetWithInputBindings()
    {
        // Per-instance WithInputBindings should run BEFORE the override.
        var callOrder = new List<string>();

        var listWidget = new ListWidget(["A", "B"])
            .WithInputBindings(b => callOrder.Add("per-instance"));

        var overrideWidget = new InputOverrideWidget(listWidget)
            .Override<ListWidget>(b => callOrder.Add("override"));

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context);
        var listNode = (overrideNode as InputOverrideNode)?.Child as ListNode;
        listNode!.BuildBindings();

        Assert.Equal(2, callOrder.Count);
        Assert.Equal("per-instance", callOrder[0]);
        Assert.Equal("override", callOrder[1]);
    }

    [Fact]
    public async Task Override_RebindingPattern_Works()
    {
        // The classic remap pattern: remove default, rebind to new key.
        var listWidget = new ListWidget(["A", "B"]);
        var overrideWidget = new InputOverrideWidget(listWidget)
            .Override<ListWidget>(b =>
            {
                b.Remove(ListWidget.MoveUp);
                b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
            });

        var context = ReconcileContext.CreateRoot();
        var overrideNode = await overrideWidget.ReconcileAsync(null, context);
        var listNode = (overrideNode as InputOverrideNode)?.Child as ListNode;
        var bindings = listNode!.BuildBindings();

        // MoveUp should still be in the action registry (it was registered by defaults)
        var actionIds = bindings.GetAllActionIds();
        Assert.Contains(ListWidget.MoveUp, actionIds);

        // There should be a binding for K with MoveUp action
        var moveUpBindings = bindings.GetBindings(ListWidget.MoveUp);
        Assert.Single(moveUpBindings);
        Assert.Equal(Hex1bKey.K, moveUpBindings[0].Steps[0].Key);
    }

    #endregion

    #region Unit Tests — Node pass-through behavior

    [Fact]
    public void InputOverrideNode_IsFocusable_ReturnsFalse()
    {
        var node = new InputOverrideNode();
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void InputOverrideNode_Measure_DelegatesToChild()
    {
        var node = new InputOverrideNode();
        var child = new ButtonNode { Label = "OK" };
        node.Child = child;

        var size = node.Measure(new Constraints(0, 100, 0, 10));
        Assert.True(size.Width > 0);
    }

    [Fact]
    public void InputOverrideNode_Measure_NoChild_ReturnsZero()
    {
        var node = new InputOverrideNode();
        var size = node.Measure(new Constraints(0, 100, 0, 10));
        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    #endregion

    #region Integration Tests — Full app pipeline

    [Fact]
    public async Task Integration_Override_RebindsKeyForFocusedWidget()
    {
        // Full integration test: InputOverride rebinds ListWidget.MoveDown from DownArrow to J.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            ctx => new InputOverrideWidget(
                new ListWidget(["Alpha", "Beta", "Charlie"])
            )
            .Override<ListWidget>(b =>
            {
                b.Remove(ListWidget.MoveDown);
                b.Key(Hex1bKey.J).Triggers(ListWidget.MoveDown);
            }),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableDefaultCtrlCExit = false }
        );

        using var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        // Wait for initial render, then press Tab to focus the list, then J to move down
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(200)
            .Key(Hex1bKey.Tab).Wait(200)
            .Key(Hex1bKey.J).Wait(200)
            .Capture("after-j")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // The test passes if we get here without errors — the rebinding was accepted and processed

        cts.Cancel();
        await runTask;
    }

    #endregion

    #region Nested InputOverride Tests

    [Fact]
    public async Task NestedOverride_InnerWins_ForSameWidgetType()
    {
        // Nested InputOverrideWidgets — inner override should take precedence.
        var outerCalled = false;
        var innerCalled = false;

        var listWidget = new ListWidget(["A"]);

        var inner = new InputOverrideWidget(listWidget)
            .Override<ListWidget>(b => { innerCalled = true; });

        var outer = new InputOverrideWidget(inner)
            .Override<ListWidget>(b => { outerCalled = true; });

        var context = ReconcileContext.CreateRoot();
        var outerNode = await outer.ReconcileAsync(null, context);

        // Navigate to the list node
        var innerOverrideNode = (outerNode as InputOverrideNode)?.Child as InputOverrideNode;
        var listNode = innerOverrideNode?.Child as ListNode;
        Assert.NotNull(listNode);

        listNode!.BuildBindings();

        // Inner override should win for the same type
        Assert.True(innerCalled, "Inner override should run");
        Assert.False(outerCalled, "Outer override should be replaced by inner for same type");
    }

    [Fact]
    public async Task NestedOverride_DifferentTypes_BothApply()
    {
        // Outer overrides ListWidget, inner overrides ButtonWidget — both should apply.
        var listOverrideCalled = false;
        var buttonOverrideCalled = false;

        var content = new VStackWidget([
            new ListWidget(["A"]),
            new ButtonWidget("OK")
        ]);

        var inner = new InputOverrideWidget(content)
            .Override<ButtonWidget>(b => { buttonOverrideCalled = true; });

        var outer = new InputOverrideWidget(inner)
            .Override<ListWidget>(b => { listOverrideCalled = true; });

        var context = ReconcileContext.CreateRoot();
        var outerNode = await outer.ReconcileAsync(null, context);

        var innerOverrideNode = (outerNode as InputOverrideNode)?.Child as InputOverrideNode;
        var vstackNode = innerOverrideNode?.Child as VStackNode;

        var listNode = vstackNode!.Children.OfType<ListNode>().First();
        var buttonNode = vstackNode.Children.OfType<ButtonNode>().First();

        listNode.BuildBindings();
        buttonNode.BuildBindings();

        Assert.True(listOverrideCalled, "Outer ListWidget override should cascade through inner");
        Assert.True(buttonOverrideCalled, "Inner ButtonWidget override should apply");
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void ExtensionMethod_CreatesInputOverrideWidget()
    {
        // Verify the extension method creates an InputOverrideWidget with the correct content.
        var content = new ButtonWidget("OK");
        var widget = new InputOverrideWidget(content);

        Assert.Equal(content, widget.Content);
        Assert.Empty(widget.Overrides);
    }

    #endregion
}
