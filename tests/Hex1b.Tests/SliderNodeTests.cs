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
public class SliderNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_FillsAvailableWidth()
    {
        var node = new SliderNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, 80, 0, 10);

        var size = node.Measure(constraints);

        Assert.Equal(80, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMinWidth()
    {
        var node = new SliderNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(20, 80, 0, 10);

        var size = node.Measure(constraints);

        Assert.Equal(80, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_UnboundedWidth_UsesDefaultWidth()
    {
        var node = new SliderNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, int.MaxValue, 0, 10);

        var size = node.Measure(constraints);

        Assert.Equal(20, size.Width);
        Assert.Equal(1, size.Height);
    }

    #endregion

    #region Reconciliation Tests

    [Fact]
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

        Assert.Same(node1, node2);
        // Value should be preserved (not changed to 75) because state is owned by node
        Assert.Equal(25, ((SliderNode)node2).Value);
    }

    [Fact]
    public void Reconcile_AppliesInitialValueOnNewNode()
    {
        var widget = new SliderWidget { InitialValue = 42, Minimum = 0, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());
        context.IsNew = true; // Simulating new node creation

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;

        Assert.Equal(42, node!.Value);
    }

    [Fact]
    public void Reconcile_ClampsValueToRange()
    {
        var widget1 = new SliderWidget { InitialValue = 150, Minimum = 0, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());
        context.IsNew = true; // Simulating new node creation

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;

        Assert.Equal(100, node!.Value); // Clamped to max
    }

    [Fact]
    public void Reconcile_MarksDirtyOnMinimumChange()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Minimum = 0, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 50, Minimum = -50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnMaximumChange()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 50, Maximum = 200 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnStepChange()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Step = 5 };
        var widget2 = new SliderWidget { InitialValue = 50, Step = 10 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_DoesNotMarkDirtyWhenUnchanged()
    {
        var widget1 = new SliderWidget { InitialValue = 50, Maximum = 100 };
        var widget2 = new SliderWidget { InitialValue = 50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as SliderNode;
        node!.ClearDirty();

        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        Assert.False(node.IsDirty);
    }

    [Fact]
    public void GetExpectedNodeType_ReturnsSliderNode()
    {
        var widget = new SliderWidget();

        Assert.Equal(typeof(SliderNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new SliderNode();

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void IsFocused_WhenSet_MarksDirty()
    {
        var node = new SliderNode();
        node.ClearDirty();

        node.IsFocused = true;

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void IsFocused_WhenSetToSameValue_DoesNotMarkDirty()
    {
        var node = new SliderNode { IsFocused = true };
        node.ClearDirty();

        node.IsFocused = true;

        Assert.False(node.IsDirty);
    }

    #endregion

    #region Percentage Calculation Tests

    [Theory]
    [InlineData(0, 0, 100, 0.0)]
    [InlineData(50, 0, 100, 0.5)]
    [InlineData(100, 0, 100, 1.0)]
    [InlineData(25, 0, 100, 0.25)]
    [InlineData(-25, -50, 50, 0.25)]
    [InlineData(0, -100, 100, 0.5)]
    [InlineData(5, 0, 10, 0.5)]
    public void Percentage_CalculatesCorrectly(double value, double min, double max, double expectedPercentage)
    {
        var node = new SliderNode
        {
            Value = value,
            Minimum = min,
            Maximum = max
        };

        Assert.Equal(expectedPercentage, node.Percentage, precision: 5);
    }

    [Fact]
    public void Percentage_EqualMinMax_ReturnsZero()
    {
        var node = new SliderNode
        {
            Value = 50,
            Minimum = 50,
            Maximum = 50
        };

        Assert.Equal(0.0, node.Percentage);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.True(node.Value > 50);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.True(node.Value < 50);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.True(node.Value > 50);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.True(node.Value < 50);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(10, node.Value);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(80, node.Value);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(60, node.Value);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(40, node.Value);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(100, node.Value);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.Value);
    }

    [Fact]
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

        Assert.Equal(10, node.Value);
    }

    [Fact]
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

        Assert.True(callbackInvoked);
        Assert.Equal(50, previousValue);
    }

    [Fact]
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

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal(50, node.Value);
    }

    #endregion

    #region Widget Event Handler Tests

    [Fact]
    public void OnValueChanged_SyncHandler_ReturnsNewWidget()
    {
        var widget = new SliderWidget();

        var newWidget = widget.OnValueChanged(_ => { });

        Assert.NotSame(widget, newWidget);
        Assert.NotNull(newWidget.ValueChangedHandler);
    }

    [Fact]
    public void OnValueChanged_AsyncHandler_ReturnsNewWidget()
    {
        var widget = new SliderWidget();

        var newWidget = widget.OnValueChanged(async args => await Task.Delay(1));

        Assert.NotSame(widget, newWidget);
        Assert.NotNull(newWidget.ValueChangedHandler);
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new SliderNode();
        var bounds = new Rect(5, 10, 40, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion
}
