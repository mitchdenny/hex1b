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
[TestClass]
public class InteractableNodeTests
{
    #region Measurement Tests

    [TestMethod]
    public void Measure_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Hello" };
        var node = new InteractableNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // ButtonNode: " Hello " = 2 + 5 = 7 (Phase 2 chip layout)
        Assert.AreEqual(7, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_NullChild_ReturnsZero()
    {
        var node = new InteractableNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void Measure_PassesConstraintsToChild()
    {
        var child = new ButtonNode { Label = "A Very Long Label" };
        var node = new InteractableNode { Child = child };
        var constraints = new Constraints(0, 10, 0, 5);

        var size = node.Measure(constraints);

        Assert.IsTrue(size.Width <= 10);
    }

    #endregion

    #region Arrange Tests

    [TestMethod]
    public void Arrange_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new InteractableNode { Child = child };
        var bounds = new Rect(5, 10, 20, 3);

        node.Arrange(bounds);

        Assert.AreEqual(bounds, node.Bounds);
        Assert.AreEqual(bounds, child.Bounds);
    }

    [TestMethod]
    public void Arrange_NullChild_DoesNotThrow()
    {
        var node = new InteractableNode { Child = null };
        var bounds = new Rect(0, 0, 10, 5);

        node.Arrange(bounds);

        Assert.AreEqual(bounds, node.Bounds);
    }

    #endregion

    #region Focus Tests

    [TestMethod]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new InteractableNode();

        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public void IsFocused_SetTrue_InvokesFocusChangedCallback()
    {
        bool? focusState = null;
        var node = new InteractableNode
        {
            FocusChangedAction = isFocused => focusState = isFocused
        };

        node.IsFocused = true;

        Assert.IsTrue(focusState);
    }

    [TestMethod]
    public void IsFocused_SetFalse_InvokesFocusChangedCallback()
    {
        bool? focusState = null;
        var node = new InteractableNode();
        node.IsFocused = true;
        node.FocusChangedAction = isFocused => focusState = isFocused;

        node.IsFocused = false;

        Assert.IsFalse(focusState);
    }

    [TestMethod]
    public void IsFocused_SetSameValue_DoesNotInvokeCallback()
    {
        var callCount = 0;
        var node = new InteractableNode
        {
            FocusChangedAction = _ => callCount++
        };

        node.IsFocused = false; // Already false, no change

        Assert.AreEqual(0, callCount);
    }

    [TestMethod]
    public void GetFocusableNodes_ReturnsOnlySelf()
    {
        // Even with a focusable child, interactable captures focus
        var child = new ButtonNode { Label = "Focusable Child" };
        var node = new InteractableNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusables);
        Assert.AreSame(node, focusables[0]);
    }

    #endregion

    #region Hover Tests

    [TestMethod]
    public void IsHovered_SetTrue_InvokesHoverChangedCallback()
    {
        bool? hoverState = null;
        var node = new InteractableNode
        {
            HoverChangedAction = isHovered => hoverState = isHovered
        };

        node.IsHovered = true;

        Assert.IsTrue(hoverState);
    }

    [TestMethod]
    public void IsHovered_SetSameValue_DoesNotInvokeCallback()
    {
        var callCount = 0;
        var node = new InteractableNode
        {
            HoverChangedAction = _ => callCount++
        };

        node.IsHovered = false; // Already false

        Assert.AreEqual(0, callCount);
    }

    #endregion

    #region Input Handling Tests

    [TestMethod]
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

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(clicked);
    }

    [TestMethod]
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

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(clicked);
    }

    [TestMethod]
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

        Assert.AreEqual(InputResult.NotHandled, result);
    }

    [TestMethod]
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

        Assert.AreEqual(InputResult.NotHandled, result);
        Assert.IsFalse(clicked);
    }

    #endregion

    #region HitTest Tests

    [TestMethod]
    public void HitTestBounds_ReturnsBounds()
    {
        var node = new InteractableNode();
        var bounds = new Rect(5, 10, 20, 3);
        node.Arrange(bounds);

        Assert.AreEqual(bounds, node.HitTestBounds);
    }

    #endregion

    #region GetChildren Tests

    [TestMethod]
    public void GetChildren_WithChild_ReturnsChild()
    {
        var child = new ButtonNode { Label = "Child" };
        var node = new InteractableNode { Child = child };

        var children = node.GetChildren().ToList();

        TestSeq.Single(children);
        Assert.AreSame(child, children[0]);
    }

    [TestMethod]
    public void GetChildren_NullChild_ReturnsEmpty()
    {
        var node = new InteractableNode { Child = null };

        var children = node.GetChildren().ToList();

        Assert.IsEmpty(children);
    }

    #endregion

    #region Widget Reconciliation Tests

    [TestMethod]
    public async Task Widget_ReconcileAsync_CreatesNode()
    {
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        TestSeq.IsType<InteractableNode>(node);
        var interactable = (InteractableNode)node;
        Assert.IsNotNull(interactable.Child);
    }

    [TestMethod]
    public async Task Widget_ReconcileAsync_ReusesExistingNode()
    {
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();
        var existingNode = new InteractableNode();

        var node = await widget.ReconcileAsync(existingNode, context);

        Assert.AreSame(existingNode, node);
    }

    [TestMethod]
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

        Assert.IsTrue(contextIsFocused);
    }

    [TestMethod]
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

        Assert.IsFalse(contextIsFocused);
        Assert.IsFalse(contextIsHovered);
    }

    [TestMethod]
    public async Task Widget_OnClick_WiresUpClickAction()
    {
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"))
            .OnClick(args => { });
        var context = ReconcileContext.CreateRoot();

        var node = (InteractableNode)await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node.ClickAction);
    }

    [TestMethod]
    public async Task Widget_OnFocusChanged_WiresUpCallback()
    {
        bool? focusState = null;
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"))
            .OnFocusChanged(args => focusState = args.IsFocused);
        var context = ReconcileContext.CreateRoot();

        var node = (InteractableNode)await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node.FocusChangedAction);
        node.IsFocused = true;
        Assert.IsTrue(focusState);
    }

    [TestMethod]
    public async Task Widget_OnHoverChanged_WiresUpCallback()
    {
        bool? hoverState = null;
        var widget = new InteractableWidget(ic => new TextBlockWidget("Hello"))
            .OnHoverChanged(args => hoverState = args.IsHovered);
        var context = ReconcileContext.CreateRoot();

        var node = (InteractableNode)await widget.ReconcileAsync(null, context);

        Assert.IsNotNull(node.HoverChangedAction);
        node.IsHovered = true;
        Assert.IsTrue(hoverState);
    }

    #endregion

    #region Extension Method Tests

    [TestMethod]
    public void Extensions_Interactable_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Interactable(ic => new TextBlockWidget("Hello"));

        TestSeq.IsType<InteractableWidget>(widget);
    }

    [TestMethod]
    public void Extensions_InteractableArray_CreatesWidgetWithVStack()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Interactable(ic => new Hex1bWidget[]
        {
            new TextBlockWidget("Line 1"),
            new TextBlockWidget("Line 2"),
        });

        TestSeq.IsType<InteractableWidget>(widget);
    }

    #endregion
}
