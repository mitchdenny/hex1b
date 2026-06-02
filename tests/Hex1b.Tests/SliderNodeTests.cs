using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Unit tests for SliderNode measurement, rendering, and input handling.
/// </summary>
[TestClass]
public class SliderNodeTests
{
    #region Measurement Tests

    [TestMethod]
    public void Measure_FillsAvailableWidth()
    {
        var node = new SliderNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, 80, 0, 10);

        var size = node.Measure(constraints);

        Assert.AreEqual(80, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_RespectsMinWidth()
    {
        var node = new SliderNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(20, 80, 0, 10);

        var size = node.Measure(constraints);

        Assert.AreEqual(80, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_UnboundedWidth_UsesDefaultWidth()
    {
        var node = new SliderNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, int.MaxValue, 0, 10);

        var size = node.Measure(constraints);

        Assert.AreEqual(20, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    #endregion

    #region Reconciliation Tests

    [TestMethod]
    public void Reconcile_PreservesNodeOnSameType()
    {
        var widget1 = new SliderWidget { InitialValue = 25, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 75, Maximum = 100 };
        var context1 = ReconcileContext.CreateRoot(new FocusRing());
        context1.IsNew = true; // Simulating first reconciliation

        var node1 = widget1.ReconcileAsync(null, context1).GetAwaiter().GetResult();
        
        // Create a new context that is not "new" for the second reconcile
        var context2 = ReconcileContext.CreateRoot(new FocusRing());
        context2.IsNew = false; // Existing node
        var node2 = widget2.ReconcileAsync(node1, context2).GetAwaiter().GetResult();

        Assert.AreSame(node1, node2);
        // Value should be preserved (not changed to 75) because state is owned by node
        Assert.AreEqual(25, ((SliderNode)node2).Value);
    }

    [TestMethod]
    public void Reconcile_AppliesInitialValueOnNewNode()
    {
        var widget = new SliderWidget { InitialValue = 42, Minimum = 0, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());
        context.IsNew = true; // Simulating new node creation

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;

        Assert.AreEqual(42, node!.Value);
    }

    [TestMethod]
    public void Reconcile_ClampsValueToRange()
    {
        var widget1 = new SliderWidget { InitialValue = 150, Minimum = 0, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());
        context.IsNew = true; // Simulating new node creation

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;

        Assert.AreEqual(100, node!.Value); // Clamped to max
    }

    [TestMethod]
    public void Reconcile_MarksDirtyOnMinimumChange()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Minimum = 0, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 50, Minimum = -50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void Reconcile_MarksDirtyOnMaximumChange()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 50, Maximum = 200 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void Reconcile_MarksDirtyOnStepChange()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Step = 5 };
        var widget2 = new SliderWidget { InitialValue = 50, Step = 10 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void Reconcile_DoesNotMarkDirtyWhenUnchanged()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.IsFalse(node.IsDirty);
    }

    [TestMethod]
    public void GetExpectedNodeType_ReturnsSliderNode()
    {
        var widget = new SliderWidget();

        Assert.AreEqual(typeof(SliderNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Focus Tests

    [TestMethod]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new SliderNode();

        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = new SliderNode();
        node.ClearDirty();

        node.IsFocused = true;

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void IsFocused_WhenSetToSameValue_DoesNotMarkDirty()
    {
        var node = new SliderNode { IsFocused = true };
        node.ClearDirty();

        node.IsFocused = true;

        Assert.IsFalse(node.IsDirty);
    }

    #endregion

    #region Percentage Calculation Tests

    [TestMethod]
    [DataRow(0, 0, 100, 0.0)]
    [DataRow(50, 0, 100, 0.5)]
    [DataRow(100, 0, 100, 1.0)]
    [DataRow(25, 0, 100, 0.25)]
    [DataRow(-25, -50, 50, 0.25)]
    [DataRow(0, -100, 100, 0.5)]
    [DataRow(5, 0, 10, 0.5)]
    public void Percentage_CalculatesCorrectly(double value, double min, double max, double expectedPercentage)
    {
        var node = new SliderNode
        {
            Value = value,
            Minimum = min,
            Maximum = max
        };

        Assert.AreEqual(expectedPercentage, node.Percentage, delta: 5);
    }

    [TestMethod]
    public void Percentage_EqualMinMax_ReturnsZero()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 50,
            Maximum = 50
        };

        Assert.AreEqual(0.0, node.Percentage);
    }

    #endregion

    #region Input Handling Tests

    [TestMethod]
    public async Task HandleInput_RightArrow_IncreasesValue()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(node.Value > 50);
    }

    [TestMethod]
    public async Task HandleInput_LeftArrow_DecreasesValue()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(node.Value < 50);
    }

    [TestMethod]
    public async Task HandleInput_UpArrow_IncreasesValue()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(node.Value > 50);
    }

    [TestMethod]
    public async Task HandleInput_DownArrow_DecreasesValue()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(node.Value < 50);
    }

    [TestMethod]
    public async Task HandleInput_Home_JumpsToMinimum()
    {
        var node = new SliderNode
        {
            Value = 75,
            Minimum = 10,
            Maximum = 100,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(10, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_End_JumpsToMaximum()
    {
        var node = new SliderNode
        {
            Value = 25,
            Minimum = 0,
            Maximum = 80,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(80, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_PageUp_IncreasesBy10Percent()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            LargeStepPercent = 10,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.PageUp, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(60, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_PageDown_DecreasesBy10Percent()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            LargeStepPercent = 10,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(40, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_RightArrow_ClampsToMax()
    {
        var node = new SliderNode
        {
            Value = 99,
            Minimum = 0,
            Maximum = 100,
            Step = 5,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(100, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_LeftArrow_ClampsToMin()
    {
        var node = new SliderNode
        {
            Value = 2,
            Minimum = 0,
            Maximum = 100,
            Step = 5,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(0, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_WithStep_SnapsToStepValues()
    {
        var node = new SliderNode
        {
            Value = 0,
            Minimum = 0,
            Maximum = 100,
            Step = 10,
            IsFocused = true
        };

        await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(10, node.Value);
    }

    [TestMethod]
    public async Task HandleInput_ValueChangedCallback_IsCalled()
    {
        var callbackInvoked = false;
        var previousValue = 0.0;

        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            IsFocused = true,
            ValueChangedAction = (ctx, prev) =>
            {
                callbackInvoked = true;
                previousValue = prev;
                return Task.CompletedTask;
            }
        };

        await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.IsTrue(callbackInvoked);
        Assert.AreEqual(50, previousValue);
    }

    [TestMethod]
    public async Task HandleInput_OtherKey_NotHandled()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 0,
            Maximum = 100,
            IsFocused = true
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.NotHandled, result);
        Assert.AreEqual(50, node.Value);
    }

    #endregion

    #region Widget Event Handler Tests

    [TestMethod]
    public void OnValueChanged_SyncHandler_ReturnsNewWidget()
    {
        var widget = new SliderWidget();

        var newWidget = widget.OnValueChanged(_ => { });

        Assert.AreNotSame(widget, newWidget);
        Assert.IsNotNull(newWidget.ValueChangedHandler);
    }

    [TestMethod]
    public void OnValueChanged_AsyncHandler_ReturnsNewWidget()
    {
        var widget = new SliderWidget();

        var newWidget = widget.OnValueChanged(async args => await Task.Delay(1));

        Assert.AreNotSame(widget, newWidget);
        Assert.IsNotNull(newWidget.ValueChangedHandler);
    }

    #endregion

    #region Layout Tests

    [TestMethod]
    public void Arrange_SetsBounds()
    {
        var node = new SliderNode();
        var bounds = new Rect(5, 10, 40, 1);

        node.Arrange(bounds);

        Assert.AreEqual(bounds, node.Bounds);
    }

    #endregion
}
