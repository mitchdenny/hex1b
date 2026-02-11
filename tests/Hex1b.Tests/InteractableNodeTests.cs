using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for InteractableNode and InteractableWidget.
/// </summary>
public class InteractableNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Hello" };
        var node = new InteractableNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // ButtonNode: "[ Hello ]" = 4 + 5 = 9
        Assert.Equal(9, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_NullChild_ReturnsZero()
    {
        var node = new InteractableNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_PassesConstraintsToChild()
    {
        var child = new ButtonNode { Label = "A Very Long Label" };
        var node = new InteractableNode { Child = child };
        var constraints = new Constraints(0, 10, 0, 5);

        var size = node.Measure(constraints);

        Assert.True(size.Width <= 10);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new InteractableNode { Child = child };
        var bounds = new Rect(5, 10, 20, 3);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
        Assert.Equal(bounds, child.Bounds);
    }

    [Fact]
    public void Arrange_NullChild_DoesNotThrow()
    {
        var node = new InteractableNode { Child = null };
        var bounds = new Rect(0, 0, 10, 5);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new InteractableNode();

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void IsFocused_SetTrue_InvokesFocusChangedCallback()
    {
        bool? focusState = null;
        var node = new InteractableNode
        {
            FocusChangedAction = isFocused => focusState = isFocused
        };

        node.IsFocused = true;

        Assert.True(focusState);
    }

    [Fact]
    public void IsFocused_SetFalse_InvokesFocusChangedCallback()
    {
        bool? focusState = null;
        var node = new InteractableNode();
        node.IsFocused = true;
        node.FocusChangedAction = isFocused => focusState = isFocused;

        node.IsFocused = false;

        Assert.False(focusState);
    }

    [Fact]
    public void IsFocused_SetSameValue_DoesNotInvokeCallback()
    {
        var callCount = 0;
        var node = new InteractableNode
        {
            FocusChangedAction = _ => callCount++
        };

        node.IsFocused = false; // Already false, no change

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsOnlySelf()
    {
        // Even with a focusable child, interactable captures focus
        var child = new ButtonNode { Label = "Focusable Child" };
        var node = new InteractableNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(node, focusables[0]);
    }

    #endregion

    #region Hover Tests

    [Fact]
    public void IsHovered_SetTrue_InvokesHoverChangedCallback()
    {
        bool? hoverState = null;
        var node = new InteractableNode
        {
            HoverChangedAction = isHovered => hoverState = isHovered
        };

        node.IsHovered = true;

        Assert.True(hoverState);
    }

    [Fact]
    public void IsHovered_SetSameValue_DoesNotInvokeCallback()
    {
        var callCount = 0;
        var node = new InteractableNode
        {
            HoverChangedAction = _ => callCount++
        };

        node.IsHovered = false; // Already false

        Assert.Equal(0, callCount);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_Enter_TriggersClickAction()
    {
        var clicked = false;
        var node = new InteractableNode
        {
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public async Task HandleInput_Space_TriggersClickAction()
    {
        var clicked = false;
        var node = new InteractableNode
        {
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.Spacebar, ' ', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public async Task HandleInput_NullClickAction_DoesNotThrow()
    {
        var node = new InteractableNode
        {
            IsFocused = true,
            ClickAction = null
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_OtherKey_DoesNotClick()
    {
        var clicked = false;
        var node = new InteractableNode
        {
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };

        var result = await InputRouter.RouteInputToNodeAsync(
            node,
            new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None),
            null, null,
            TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
        Assert.False(clicked);
    }

    #endregion

    #region HitTest Tests

    [Fact]
    public void HitTestBounds_ReturnsBounds()
    {
        var node = new InteractableNode();
        var bounds = new Rect(5, 10, 20, 3);
        node.Arrange(bounds);

        Assert.Equal(bounds, node.HitTestBounds);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_WithChild_ReturnsChild()
    {
        var child = new ButtonNode { Label = "Child" };
        var node = new InteractableNode { Child = child };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void GetChildren_NullChild_ReturnsEmpty()
    {
        var node = new InteractableNode { Child = null };

        var children = node.GetChildren().ToList();

        Assert.Empty(children);
    }

    #endregion

    #region Widget Reconciliation Tests

    [Fact]
    public async Task Widget_ReconcileAsync_CreatesNode()
    {
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        Assert.IsType<InteractableNode>(node);
        var interactable = (InteractableNode)node;
        Assert.NotNull(interactable.Child);
    }

    [Fact]
    public async Task Widget_ReconcileAsync_ReusesExistingNode()
    {
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();
        var existingNode = new InteractableNode();

        var node = await widget.ReconcileAsync(existingNode, context);

        Assert.Same(existingNode, node);
    }

    [Fact]
    public async Task Widget_ReconcileAsync_ContextReflectsNodeState()
    {
        bool? contextIsFocused = null;
        var widget = new InteractableWidget(ic =>
        {
            contextIsFocused = ic.IsFocused;
            return new TextBlockWidget("Hello");
        });
        var context = ReconcileContext.CreateRoot();
        var existingNode = new InteractableNode();
        existingNode.IsFocused = true;

        await widget.ReconcileAsync(existingNode, context);

        Assert.True(contextIsFocused);
    }

    [Fact]
    public async Task Widget_ReconcileAsync_ContextDefaultsFalse()
    {
        bool? contextIsFocused = null;
        bool? contextIsHovered = null;
        var widget = new InteractableWidget(ic =>
        {
            contextIsFocused = ic.IsFocused;
            contextIsHovered = ic.IsHovered;
            return new TextBlockWidget("Hello");
        });
        var context = ReconcileContext.CreateRoot();

        await widget.ReconcileAsync(null, context);

        Assert.False(contextIsFocused);
        Assert.False(contextIsHovered);
    }

    [Fact]
    public async Task Widget_OnClick_WiresUpClickAction()
    {
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"))
            .OnClick(args => { });
        var context = ReconcileContext.CreateRoot();

        var node = (InteractableNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.ClickAction);
    }

    [Fact]
    public async Task Widget_OnFocusChanged_WiresUpCallback()
    {
        bool? focusState = null;
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"))
            .OnFocusChanged(args => focusState = args.IsFocused);
        var context = ReconcileContext.CreateRoot();

        var node = (InteractableNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.FocusChangedAction);
        node.IsFocused = true;
        Assert.True(focusState);
    }

    [Fact]
    public async Task Widget_OnHoverChanged_WiresUpCallback()
    {
        bool? hoverState = null;
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"))
            .OnHoverChanged(args => hoverState = args.IsHovered);
        var context = ReconcileContext.CreateRoot();

        var node = (InteractableNode)await widget.ReconcileAsync(null, context);

        Assert.NotNull(node.HoverChangedAction);
        node.IsHovered = true;
        Assert.True(hoverState);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void Extensions_Interactable_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Interactable(ic => new TextBlockWidget("Hello"));

        Assert.IsType<InteractableWidget>(widget);
    }

    [Fact]
    public void Extensions_InteractableArray_CreatesWidgetWithVStack()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Interactable(ic => new Hex1bWidget[]
        {
            new TextBlockWidget("Line 1"),
            new TextBlockWidget("Line 2"),
        });

        Assert.IsType<InteractableWidget>(widget);
    }

    #endregion
}
