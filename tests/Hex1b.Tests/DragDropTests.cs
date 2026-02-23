using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for DraggableNode, DraggableWidget, DroppableNode, DroppableWidget,
/// DragDropManager, and the drag-drop extension methods.
/// </summary>
public class DragDropTests
{
    #region DraggableNode Tests

    [Fact]
    public void DraggableNode_IsFocusable_ReturnsTrue()
    {
        var node = new DraggableNode();
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void DraggableNode_IsDragging_DefaultFalse()
    {
        var node = new DraggableNode();
        Assert.False(node.IsDragging);
    }

    [Fact]
    public void DraggableNode_IsDragging_SetTrue_MarksDirty()
    {
        var node = new DraggableNode();
        node.IsDragging = true;
        Assert.True(node.IsDragging);
    }

    [Fact]
    public void DraggableNode_IsDragging_SetSameValue_NoChange()
    {
        var node = new DraggableNode();
        node.IsDragging = false; // Already false
        Assert.False(node.IsDragging);
    }

    [Fact]
    public void DraggableNode_Measure_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Drag Me" };
        var node = new DraggableNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // ButtonNode: "[ Drag Me ]" = 4 + 7 = 11
        Assert.Equal(11, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void DraggableNode_Measure_NullChild_ReturnsZero()
    {
        var node = new DraggableNode { Child = null };
        var size = node.Measure(Constraints.Unbounded);
        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void DraggableNode_Arrange_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new DraggableNode { Child = child };
        node.Measure(Constraints.Unbounded);
        var rect = new Rect(5, 10, 20, 3);

        node.Arrange(rect);

        Assert.Equal(rect, child.Bounds);
    }

    [Fact]
    public void DraggableNode_GetChildren_ReturnsChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new DraggableNode { Child = child };

        var children = node.GetChildren();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void DraggableNode_GetChildren_NullChild_ReturnsEmpty()
    {
        var node = new DraggableNode { Child = null };
        Assert.Empty(node.GetChildren());
    }

    [Fact]
    public void DraggableNode_GetFocusableNodes_ReturnsOnlySelf()
    {
        var child = new ButtonNode { Label = "Focusable" };
        var node = new DraggableNode { Child = child };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(node, focusables[0]);
    }

    [Fact]
    public void DraggableNode_HitTestBounds_EqualsBounds()
    {
        var node = new DraggableNode { Child = new ButtonNode { Label = "Test" } };
        node.Measure(Constraints.Unbounded);
        var rect = new Rect(0, 0, 10, 1);
        node.Arrange(rect);

        Assert.Equal(rect, node.HitTestBounds);
    }

    [Fact]
    public void DraggableNode_DragData_CanBeSet()
    {
        var data = new { Id = "task-1", Name = "Test Task" };
        var node = new DraggableNode { DragData = data };
        Assert.Same(data, node.DragData);
    }

    #endregion

    #region DroppableNode Tests

    [Fact]
    public void DroppableNode_IsFocusable_ReturnsFalse()
    {
        var node = new DroppableNode();
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void DroppableNode_IsHoveredByDrag_DefaultFalse()
    {
        var node = new DroppableNode();
        Assert.False(node.IsHoveredByDrag);
    }

    [Fact]
    public void DroppableNode_IsHoveredByDrag_SetTrue_MarksDirty()
    {
        var node = new DroppableNode();
        node.IsHoveredByDrag = true;
        Assert.True(node.IsHoveredByDrag);
    }

    [Fact]
    public void DroppableNode_Accepts_NoPredicate_AcceptsAll()
    {
        var node = new DroppableNode { AcceptPredicate = null };
        Assert.True(node.Accepts("anything"));
        Assert.True(node.Accepts(42));
    }

    [Fact]
    public void DroppableNode_Accepts_WithPredicate_FiltersCorrectly()
    {
        var node = new DroppableNode
        {
            AcceptPredicate = data => data is string s && s.StartsWith("task")
        };

        Assert.True(node.Accepts("task-1"));
        Assert.False(node.Accepts("bug-1"));
        Assert.False(node.Accepts(42));
    }

    [Fact]
    public void DroppableNode_Measure_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Drop" };
        var node = new DroppableNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(8, size.Width); // "[ Drop ]"
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void DroppableNode_GetFocusableNodes_ReturnsFocusableChildren()
    {
        var button = new ButtonNode { Label = "Click" };
        var node = new DroppableNode { Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        // DroppableNode delegates to children
        Assert.Single(focusables);
        Assert.Same(button, focusables[0]);
    }

    [Fact]
    public void DroppableNode_GetFocusableNodes_NullChild_ReturnsEmpty()
    {
        var node = new DroppableNode { Child = null };
        Assert.Empty(node.GetFocusableNodes());
    }

    #endregion

    #region DragDropManager Tests

    [Fact]
    public void DragDropManager_IsDragging_DefaultFalse()
    {
        var manager = new DragDropManager();
        Assert.False(manager.IsDragging);
    }

    [Fact]
    public void DragDropManager_StartDrag_SetsState()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };

        manager.StartDrag(source, "test-data", 10, 20);

        Assert.True(manager.IsDragging);
        Assert.Same(source, manager.ActiveSource);
        Assert.Equal("test-data", manager.ActiveDragData);
        Assert.Equal(10, manager.DragX);
        Assert.Equal(20, manager.DragY);
        Assert.Null(manager.HoveredTarget);
    }

    [Fact]
    public void DragDropManager_UpdatePosition_UpdatesState()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };
        var target = new DroppableNode();

        manager.StartDrag(source, "data", 0, 0);
        manager.UpdatePosition(15, 25, target);

        Assert.Equal(15, manager.DragX);
        Assert.Equal(25, manager.DragY);
        Assert.Same(target, manager.HoveredTarget);
    }

    [Fact]
    public void DragDropManager_EndDrag_ClearsState()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };
        var target = new DroppableNode();

        manager.StartDrag(source, "data", 10, 20);
        manager.UpdatePosition(15, 25, target);
        manager.EndDrag();

        Assert.False(manager.IsDragging);
        Assert.Null(manager.ActiveSource);
        Assert.Null(manager.ActiveDragData);
        Assert.Null(manager.HoveredTarget);
    }

    #endregion

    #region Widget Reconciliation Tests

    [Fact]
    public void DraggableWidget_GetExpectedNodeType_ReturnsDraggableNode()
    {
        var widget = new DraggableWidget("drag-data", dc => new TextBlockWidget("Test"));
        Assert.Equal(typeof(DraggableNode), widget.GetExpectedNodeType());
    }

    [Fact]
    public void DroppableWidget_GetExpectedNodeType_ReturnsDroppableNode()
    {
        var widget = new DroppableWidget(dc => new TextBlockWidget("Target"));
        Assert.Equal(typeof(DroppableNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Context Tests

    [Fact]
    public void DraggableContext_IsDragging_ReflectsNodeState()
    {
        var node = new DraggableNode { DragData = "test" };
        var context = new DraggableContext(node);

        Assert.False(context.IsDragging);

        node.IsDragging = true;
        Assert.True(context.IsDragging);
    }

    [Fact]
    public void DraggableContext_DragData_ReflectsNodeState()
    {
        var node = new DraggableNode { DragData = "my-data" };
        var context = new DraggableContext(node);

        Assert.Equal("my-data", context.DragData);
    }

    [Fact]
    public void DroppableContext_IsHoveredByDrag_ReflectsNodeState()
    {
        var node = new DroppableNode();
        var context = new DroppableContext(node);

        Assert.False(context.IsHoveredByDrag);

        node.IsHoveredByDrag = true;
        Assert.True(context.IsHoveredByDrag);
    }

    [Fact]
    public void DroppableContext_CanAcceptDrag_ReflectsNodeState()
    {
        var node = new DroppableNode { CanAcceptDrag = true };
        var context = new DroppableContext(node);
        Assert.True(context.CanAcceptDrag);
    }

    [Fact]
    public void DroppableContext_HoveredDragData_ReflectsNodeState()
    {
        var node = new DroppableNode { HoveredDragData = "test-data" };
        var context = new DroppableContext(node);
        Assert.Equal("test-data", context.HoveredDragData);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void Draggable_Extension_CreatesDraggableWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Draggable("data", dc => new TextBlockWidget("Content"));

        Assert.IsType<DraggableWidget>(widget);
        Assert.Equal("data", widget.DragData);
    }

    [Fact]
    public void Draggable_ArrayExtension_CreatesDraggableWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Draggable("data", dc => new Hex1bWidget[]
        {
            new TextBlockWidget("Line 1"),
            new TextBlockWidget("Line 2"),
        });

        Assert.IsType<DraggableWidget>(widget);
    }

    [Fact]
    public void Droppable_Extension_CreatesDroppableWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Droppable(dc => new TextBlockWidget("Target"));

        Assert.IsType<DroppableWidget>(widget);
    }

    [Fact]
    public void DraggableWidget_DragOverlay_SetsBuilder()
    {
        var widget = new DraggableWidget("data", dc => new TextBlockWidget("Content"))
            .DragOverlay(dc => new TextBlockWidget("Ghost"));

        Assert.NotNull(widget.DragOverlayBuilder);
    }

    [Fact]
    public void DroppableWidget_Accept_SetsPredicate()
    {
        var widget = new DroppableWidget(dc => new TextBlockWidget("Target"))
            .Accept(data => data is string);

        Assert.NotNull(widget.AcceptPredicate);
    }

    [Fact]
    public void DroppableWidget_OnDrop_SetsHandler()
    {
        var widget = new DroppableWidget(dc => new TextBlockWidget("Target"))
            .OnDrop(e => { });

        Assert.NotNull(widget.DropHandler);
    }

    #endregion

    #region Drag Overlay Tests

    [Fact]
    public void DragDropManager_BuildOverlayWidget_NoDrag_ReturnsNull()
    {
        var manager = new DragDropManager();
        Assert.Null(manager.BuildOverlayWidget());
    }

    [Fact]
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
        Assert.Null(manager.BuildOverlayWidget());
    }

    [Fact]
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
        Assert.NotNull(overlay);
        Assert.IsType<DragOverlayWidget>(overlay);
    }

    [Fact]
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
        Assert.Equal(30, overlay.CursorX);
        Assert.Equal(15, overlay.CursorY);
    }

    [Fact]
    public void DragOverlayNode_IsFocusable_ReturnsFalse()
    {
        var node = new DragOverlayNode();
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void DragOverlayNode_GetFocusableNodes_ReturnsEmpty()
    {
        var child = new ButtonNode { Label = "Ghost" };
        var node = new DragOverlayNode { Child = child };
        Assert.Empty(node.GetFocusableNodes());
    }

    [Fact]
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
        Assert.Equal(11, child.Bounds.X);
        Assert.Equal(5, child.Bounds.Y);
    }

    #endregion

    #region DropTargetNode Tests

    [Fact]
    public void DropTargetNode_IsNotFocusable()
    {
        var node = new DropTargetNode();
        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void DropTargetNode_IsActive_DefaultFalse()
    {
        var node = new DropTargetNode();
        Assert.False(node.IsActive);
    }

    [Fact]
    public void DropTargetNode_Measure_WhenInactive_ReturnsZeroHeight()
    {
        var node = new DropTargetNode();
        var child = new TextBlockNode { Text = "indicator" };
        node.Child = child;

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        Assert.Equal(0, size.Height);
        Assert.Equal(40, size.Width);
    }

    [Fact]
    public void DropTargetNode_Measure_WhenActive_MeasuresChild()
    {
        var node = new DropTargetNode();
        var child = new TextBlockNode { Text = "drop here" };
        node.Child = child;
        node.IsActive = true;

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        Assert.Equal(1, size.Height); // TextBlock is 1 row
    }

    [Fact]
    public void DropTargetNode_TargetId_CanBeSet()
    {
        var node = new DropTargetNode { TargetId = "after-card-2" };
        Assert.Equal("after-card-2", node.TargetId);
    }

    [Fact]
    public void DropTargetNode_GetFocusableNodes_ReturnsEmpty()
    {
        var node = new DropTargetNode { Child = new TextBlockNode() };
        Assert.Empty(node.GetFocusableNodes());
    }

    #endregion

    #region DropTarget Proximity Tests

    [Fact]
    public void DroppableNode_FindDropTargets_FindsDescendants()
    {
        var droppable = new DroppableNode();
        var vstack = new VStackNode();
        var dt1 = new DropTargetNode { TargetId = "pos-1" };
        var dt2 = new DropTargetNode { TargetId = "pos-2" };
        vstack.Children = [dt1, new TextBlockNode(), dt2];
        droppable.Child = vstack;

        var targets = droppable.FindDropTargets();

        Assert.Equal(2, targets.Count);
        Assert.Equal("pos-1", targets[0].TargetId);
        Assert.Equal("pos-2", targets[1].TargetId);
    }

    [Fact]
    public void DroppableNode_FindDropTargets_EmptyWhenNone()
    {
        var droppable = new DroppableNode();
        droppable.Child = new TextBlockNode();

        var targets = droppable.FindDropTargets();

        Assert.Empty(targets);
    }

    [Fact]
    public void DragDropManager_ActiveDropTarget_ClearedOnEndDrag()
    {
        var manager = new DragDropManager();
        var source = new DraggableNode { DragData = "test" };
        manager.StartDrag(source, "test", 0, 0);
        manager.ActiveDropTarget = new DropTargetNode { TargetId = "pos-1", IsActive = true };

        manager.EndDrag();

        Assert.Null(manager.ActiveDropTarget);
    }

    #endregion

    #region DropTargetContext Tests

    [Fact]
    public void DropTargetContext_IsActive_ReflectsNodeState()
    {
        var node = new DropTargetNode { TargetId = "test" };
        var ctx = new DropTargetContext(node);

        Assert.False(ctx.IsActive);

        node.IsActive = true;
        Assert.True(ctx.IsActive);
    }

    [Fact]
    public void DropTargetContext_DragData_ReflectsNodeState()
    {
        var node = new DropTargetNode { TargetId = "test" };
        var ctx = new DropTargetContext(node);

        Assert.Null(ctx.DragData);

        node.DragData = "some-data";
        Assert.Equal("some-data", ctx.DragData);
    }

    #endregion
}
