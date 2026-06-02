using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for DraggableNode, DraggableWidget, DroppableNode, DroppableWidget,
/// DragDropManager, and the drag-drop extension methods.
/// </summary>
[TestClass]
public class DragDropTests
{
    #region DraggableNode Tests

    [TestMethod]
    public void DraggableNode_IsFocusable_ReturnsTrue()
    {
        var node = new DraggableNode();
        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public void DraggableNode_IsDragging_DefaultFalse()
    {
        var node = new DraggableNode();
        Assert.IsFalse(node.IsDragging);
    }

    [TestMethod]
    public void DraggableNode_IsDragging_SetTrue_MarksDirty()
    {
        var node = new DraggableNode();
        node.IsDragging = true;
        Assert.IsTrue(node.IsDragging);
    }

    [TestMethod]
    public void DraggableNode_IsDragging_SetSameValue_NoChange()
    {
        var node = new DraggableNode();
        node.IsDragging = false; // Already false
        Assert.IsFalse(node.IsDragging);
    }

    [TestMethod]
    public void DraggableNode_Measure_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Drag Me" };
        var node = new DraggableNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // ButtonNode: " Drag Me " = 2 + 7 = 9 (Phase 2 chip layout)
        Assert.AreEqual(9, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void DraggableNode_Measure_NullChild_ReturnsZero()
    {
        var node = new DraggableNode { Child = null };
        var size = node.Measure(Constraints.Unbounded);
        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void DraggableNode_Arrange_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new DraggableNode { Child = child };
        node.Measure(Constraints.Unbounded);
        var rect = new Rect(5, 10, 20, 3);

        node.Arrange(rect);

        Assert.AreEqual(rect, child.Bounds);
    }

    [TestMethod]
    public void DraggableNode_GetChildren_ReturnsChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new DraggableNode { Child = child };

        var children = node.GetChildren();

        TestSeq.Single(children);
        Assert.AreSame(child, children[0]);
    }

    [TestMethod]
    public void DraggableNode_GetChildren_NullChild_ReturnsEmpty()
    {
        var node = new DraggableNode { Child = null };
        Assert.IsEmpty(node.GetChildren());
    }

    [TestMethod]
    public void DraggableNode_GetFocusableNodes_ReturnsOnlySelf()
    {
        var child = new ButtonNode { Label = "Focusable" };
        var node = new DraggableNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusables);
        Assert.AreSame(node, focusables[0]);
    }

    [TestMethod]
    public void DraggableNode_HitTestBounds_EqualsBounds()
    {
        var node = new DraggableNode { Child = new ButtonNode { Label = "Test" } };
        node.Measure(Constraints.Unbounded);
        var rect = new Rect(0, 0, 10, 1);
        node.Arrange(rect);

        Assert.AreEqual(rect, node.HitTestBounds);
    }

    [TestMethod]
    public void DraggableNode_DragData_CanBeSet()
    {
        var data = new { Id = "task-1", Name = "Test Task" };
        var node = new DraggableNode { DragData = data };
        Assert.AreSame(data, node.DragData);
    }

    #endregion

    #region DroppableNode Tests

    [TestMethod]
    public void DroppableNode_IsFocusable_ReturnsFalse()
    {
        var node = new DroppableNode();
        Assert.IsFalse(node.IsFocusable);
    }

    [TestMethod]
    public void DroppableNode_IsHoveredByDrag_DefaultFalse()
    {
        var node = new DroppableNode();
        Assert.IsFalse(node.IsHoveredByDrag);
    }

    [TestMethod]
    public void DroppableNode_IsHoveredByDrag_SetTrue_MarksDirty()
    {
        var node = new DroppableNode();
        node.IsHoveredByDrag = true;
        Assert.IsTrue(node.IsHoveredByDrag);
    }

    [TestMethod]
    public void DroppableNode_Accepts_NoPredicate_AcceptsAll()
    {
        var node = new DroppableNode { AcceptPredicate = null };
        Assert.IsTrue(node.Accepts("anything"));
        Assert.IsTrue(node.Accepts(42));
    }

    [TestMethod]
    public void DroppableNode_Accepts_WithPredicate_FiltersCorrectly()
    {
        var node = new DroppableNode
        {
            AcceptPredicate = data => data is string s && s.StartsWith("task")
        };

        Assert.IsTrue(node.Accepts("task-1"));
        Assert.IsFalse(node.Accepts("bug-1"));
        Assert.IsFalse(node.Accepts(42));
    }

    [TestMethod]
    public void DroppableNode_Measure_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Drop" };
        var node = new DroppableNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(6, size.Width); // " Drop " (Phase 2 chip layout)
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void DroppableNode_GetFocusableNodes_ReturnsFocusableChildren()
    {
        var button = new ButtonNode { Label = "Click" };
        var node = new DroppableNode { Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        // DroppableNode delegates to children
        TestSeq.Single(focusables);
        Assert.AreSame(button, focusables[0]);
    }

    [TestMethod]
    public void DroppableNode_GetFocusableNodes_NullChild_ReturnsEmpty()
    {
        var node = new DroppableNode { Child = null };
        Assert.IsEmpty(node.GetFocusableNodes());
    }

    #endregion

    #region DragDropManager Tests

    [TestMethod]
    public void DragDropManager_IsDragging_DefaultFalse()
    {
        var manager = new DragDropManager();
        Assert.IsFalse(manager.IsDragging);
    }

    [TestMethod]
    public void DragDropManager_StartDrag_SetsState()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };

        manager.StartDrag(source, "test-data", 10, 20);

        Assert.IsTrue(manager.IsDragging);
        Assert.AreSame(source, manager.ActiveSource);
        Assert.AreEqual("test-data", manager.ActiveDragData);
        Assert.AreEqual(10, manager.DragX);
        Assert.AreEqual(20, manager.DragY);
        Assert.IsNull(manager.HoveredTarget);
    }

    [TestMethod]
    public void DragDropManager_UpdatePosition_UpdatesState()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };
        var target = new DroppableNode();

        manager.StartDrag(source, "data", 0, 0);
        manager.UpdatePosition(15, 25, target);

        Assert.AreEqual(15, manager.DragX);
        Assert.AreEqual(25, manager.DragY);
        Assert.AreSame(target, manager.HoveredTarget);
    }

    [TestMethod]
    public void DragDropManager_EndDrag_ClearsState()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };
        var target = new DroppableNode();

        manager.StartDrag(source, "data", 10, 20);
        manager.UpdatePosition(15, 25, target);
        manager.EndDrag();

        Assert.IsFalse(manager.IsDragging);
        Assert.IsNull(manager.ActiveSource);
        Assert.IsNull(manager.ActiveDragData);
        Assert.IsNull(manager.HoveredTarget);
    }

    #endregion

    #region Widget Reconciliation Tests

    [TestMethod]
    public void DraggableWidget_GetExpectedNodeType_ReturnsDraggableNode()
    {
        var widget = new DraggableWidget("drag-data", dc => new TextBlockWidget("Test"));
        Assert.AreEqual(typeof(DraggableNode), widget.GetExpectedNodeType());
    }

    [TestMethod]
    public void DroppableWidget_GetExpectedNodeType_ReturnsDroppableNode()
    {
        var widget = new DroppableWidget(dc => new TextBlockWidget("Target"));
        Assert.AreEqual(typeof(DroppableNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Context Tests

    [TestMethod]
    public void DraggableContext_IsDragging_ReflectsNodeState()
    {
        var node = new DraggableNode { DragData = "test" };
        var context = new DraggableContext(node);

        Assert.IsFalse(context.IsDragging);

        node.IsDragging = true;
        Assert.IsTrue(context.IsDragging);
    }

    [TestMethod]
    public void DraggableContext_DragData_ReflectsNodeState()
    {
        var node = new DraggableNode { DragData = "my-data" };
        var context = new DraggableContext(node);

        Assert.AreEqual("my-data", context.DragData);
    }

    [TestMethod]
    public void DroppableContext_IsHoveredByDrag_ReflectsNodeState()
    {
        var node = new DroppableNode();
        var context = new DroppableContext(node);

        Assert.IsFalse(context.IsHoveredByDrag);

        node.IsHoveredByDrag = true;
        Assert.IsTrue(context.IsHoveredByDrag);
    }

    [TestMethod]
    public void DroppableContext_CanAcceptDrag_ReflectsNodeState()
    {
        var node = new DroppableNode { CanAcceptDrag = true };
        var context = new DroppableContext(node);
        Assert.IsTrue(context.CanAcceptDrag);
    }

    [TestMethod]
    public void DroppableContext_HoveredDragData_ReflectsNodeState()
    {
        var node = new DroppableNode { HoveredDragData = "test-data" };
        var context = new DroppableContext(node);
        Assert.AreEqual("test-data", context.HoveredDragData);
    }

    #endregion

    #region Extension Method Tests

    [TestMethod]
    public void Draggable_Extension_CreatesDraggableWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Draggable("data", dc => new TextBlockWidget("Content"));

        TestSeq.IsType<DraggableWidget>(widget);
        Assert.AreEqual("data", widget.DragData);
    }

    [TestMethod]
    public void Draggable_ArrayExtension_CreatesDraggableWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Draggable("data", dc => new Hex1bWidget[]
        {
            new TextBlockWidget("Line 1"),
            new TextBlockWidget("Line 2"),
        });

        TestSeq.IsType<DraggableWidget>(widget);
    }

    [TestMethod]
    public void Droppable_Extension_CreatesDroppableWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Droppable(dc => new TextBlockWidget("Target"));

        TestSeq.IsType<DroppableWidget>(widget);
    }

    [TestMethod]
    public void DraggableWidget_DragOverlay_SetsBuilder()
    {
        var widget = new DraggableWidget("data", dc => new TextBlockWidget("Content"))
            .DragOverlay(dc => new TextBlockWidget("Ghost"));

        Assert.IsNotNull(widget.DragOverlayBuilder);
    }

    [TestMethod]
    public void DroppableWidget_Accept_SetsPredicate()
    {
        var widget = new DroppableWidget(dc => new TextBlockWidget("Target"))
            .Accept(data => data is string);

        Assert.IsNotNull(widget.AcceptPredicate);
    }

    [TestMethod]
    public void DroppableWidget_OnDrop_SetsHandler()
    {
        var widget = new DroppableWidget(dc => new TextBlockWidget("Target"))
            .OnDrop(e => { });

        Assert.IsNotNull(widget.DropHandler);
    }

    #endregion

    #region Drag Overlay Tests

    [TestMethod]
    public void DragDropManager_BuildOverlayWidget_NoDrag_ReturnsNull()
    {
        var manager = new DragDropManager();
        Assert.IsNull(manager.BuildOverlayWidget());
    }

    [TestMethod]
    public void DragDropManager_BuildOverlayWidget_NoBuilder_ReturnsNull()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode
        {
            DragData = "test",
            SourceWidget = new DraggableWidget("test", dc => new TextBlockWidget("Content"))
        };

        manager.StartDrag(source, "test", 10, 20);

        // No DragOverlayBuilder set on the widget
        Assert.IsNull(manager.BuildOverlayWidget());
    }

    [TestMethod]
    public void DragDropManager_BuildOverlayWidget_WithBuilder_ReturnsOverlay()
    {
        var manager = new DragDropManager();
        var widget = new DraggableWidget("test", dc => new TextBlockWidget("Content"))
            .DragOverlay(dc => new TextBlockWidget("Ghost"));
        var source = new DraggableNode
        {
            DragData = "test",
            SourceWidget = widget
        };

        manager.StartDrag(source, "test", 10, 20);

        var overlay = manager.BuildOverlayWidget();
        Assert.IsNotNull(overlay);
        TestSeq.IsType<DragOverlayWidget>(overlay);
    }

    [TestMethod]
    public void DragDropManager_BuildOverlayWidget_UsesCurrentPosition()
    {
        var manager = new DragDropManager();
        var widget = new DraggableWidget("test", dc => new TextBlockWidget("Content"))
            .DragOverlay(dc => new TextBlockWidget("Ghost"));
        var source = new DraggableNode
        {
            DragData = "test",
            SourceWidget = widget
        };

        manager.StartDrag(source, "test", 5, 10);
        manager.UpdatePosition(30, 15, null);

        var overlay = (DragOverlayWidget)manager.BuildOverlayWidget()!;
        Assert.AreEqual(30, overlay.CursorX);
        Assert.AreEqual(15, overlay.CursorY);
    }

    [TestMethod]
    public void DragOverlayNode_IsFocusable_ReturnsFalse()
    {
        var node = new DragOverlayNode();
        Assert.IsFalse(node.IsFocusable);
    }

    [TestMethod]
    public void DragOverlayNode_GetFocusableNodes_ReturnsEmpty()
    {
        var child = new ButtonNode { Label = "Ghost" };
        var node = new DragOverlayNode { Child = child };
        Assert.IsEmpty(node.GetFocusableNodes());
    }

    [TestMethod]
    public void DragOverlayNode_Arrange_PositionsAtCursorWithOffset()
    {
        var child = new TextBlockNode { Text = "Ghost" };
        var node = new DragOverlayNode
        {
            Child = child,
            CursorX = 10,
            CursorY = 5
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Child should be positioned at cursor + 1 offset
        Assert.AreEqual(11, child.Bounds.X);
        Assert.AreEqual(5, child.Bounds.Y);
    }

    #endregion

    #region DropTargetNode Tests

    [TestMethod]
    public void DropTargetNode_IsNotFocusable()
    {
        var node = new DropTargetNode();
        Assert.IsFalse(node.IsFocusable);
    }

    [TestMethod]
    public void DropTargetNode_IsActive_DefaultFalse()
    {
        var node = new DropTargetNode();
        Assert.IsFalse(node.IsActive);
    }

    [TestMethod]
    public void DropTargetNode_Measure_WhenInactive_MeasuresChild()
    {
        var node = new DropTargetNode();
        var child = new TextBlockNode { Text = "indicator" };
        node.Child = child;

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        // Inactive node still measures child — visibility is controlled by builder callback
        Assert.AreEqual(1, size.Height);
        Assert.IsTrue(size.Width > 0);
    }

    [TestMethod]
    public void DropTargetNode_Measure_WhenActive_MeasuresChild()
    {
        var node = new DropTargetNode();
        var child = new TextBlockNode { Text = "drop here" };
        node.Child = child;
        node.IsActive = true;

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        Assert.AreEqual(1, size.Height); // TextBlock is 1 row
    }

    [TestMethod]
    public void DropTargetNode_TargetId_CanBeSet()
    {
        var node = new DropTargetNode { TargetId = "after-card-2" };
        Assert.AreEqual("after-card-2", node.TargetId);
    }

    [TestMethod]
    public void DropTargetNode_GetFocusableNodes_ReturnsEmpty()
    {
        var node = new DropTargetNode { Child = new TextBlockNode() };
        Assert.IsEmpty(node.GetFocusableNodes());
    }

    #endregion

    #region DropTarget Proximity Tests

    [TestMethod]
    public void DroppableNode_FindDropTargets_FindsDescendants()
    {
        var droppable = new DroppableNode();
        var vstack = new VStackNode();
        var dt1 = new DropTargetNode { TargetId = "pos-1" };
        var dt2 = new DropTargetNode { TargetId = "pos-2" };
        vstack.Children = [dt1, new TextBlockNode(), dt2];
        droppable.Child = vstack;

        var targets = droppable.FindDropTargets();

        Assert.AreEqual(2, targets.Count);
        Assert.AreEqual("pos-1", targets[0].TargetId);
        Assert.AreEqual("pos-2", targets[1].TargetId);
    }

    [TestMethod]
    public void DroppableNode_FindDropTargets_EmptyWhenNone()
    {
        var droppable = new DroppableNode();
        droppable.Child = new TextBlockNode();

        var targets = droppable.FindDropTargets();

        Assert.IsEmpty(targets);
    }

    [TestMethod]
    public void DragDropManager_ActiveDropTarget_ClearedOnEndDrag()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };
        manager.StartDrag(source, "test", 0, 0);
        manager.ActiveDropTarget = new DropTargetNode { TargetId = "pos-1", IsActive = true };

        manager.EndDrag();

        Assert.IsNull(manager.ActiveDropTarget);
    }

    #endregion

    #region DropTargetContext Tests

    [TestMethod]
    public void DropTargetContext_IsActive_ReflectsNodeState()
    {
        var node = new DropTargetNode { TargetId = "test" };
        var ctx = new DropTargetContext(node);

        Assert.IsFalse(ctx.IsActive);

        node.IsActive = true;
        Assert.IsTrue(ctx.IsActive);
    }

    [TestMethod]
    public void DropTargetContext_DragData_ReflectsNodeState()
    {
        var node = new DropTargetNode { TargetId = "test" };
        var ctx = new DropTargetContext(node);

        Assert.IsNull(ctx.DragData);

        node.DragData = "some-data";
        Assert.AreEqual("some-data", ctx.DragData);
    }

    #endregion
}
