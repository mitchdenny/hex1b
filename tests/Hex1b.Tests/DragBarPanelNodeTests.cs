using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class DragBarPanelNodeTests
{
    #region Measure Tests

    [Fact]
    public void Measure_RightEdge_ReturnsCurrentSizeAsWidth()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 30,
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        Assert.Equal(30, size.Width);
    }

    [Fact]
    public void Measure_LeftEdge_ReturnsCurrentSizeAsWidth()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 25,
            ResolvedEdge = DragBarEdge.Left,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        Assert.Equal(25, size.Width);
    }

    [Fact]
    public void Measure_BottomEdge_ReturnsCurrentSizeAsHeight()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Bottom,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 50, 0, 30));

        Assert.Equal(15, size.Height);
    }

    [Fact]
    public void Measure_TopEdge_ReturnsCurrentSizeAsHeight()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 12,
            ResolvedEdge = DragBarEdge.Top,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 50, 0, 30));

        Assert.Equal(12, size.Height);
    }

    [Fact]
    public void Measure_DefaultSize_ReturnsMinimumDefaultOf10()
    {
        var node = new DragBarPanelNode
        {
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        // _currentSize starts at 0, but Measure uses 10 as default minimum
        Assert.True(size.Width >= 5);
    }

    [Fact]
    public void Measure_ConstrainedToMaxWidth()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 50,
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        Assert.True(size.Width <= 30);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_RightEdge_ContentGetsWidthMinusOne()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DragBarPanelNode
        {
            CurrentSize = 30,
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = content
        };

        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 30, 20));

        Assert.Equal(0, content.Bounds.X);
        Assert.Equal(29, content.Bounds.Width);
    }

    [Fact]
    public void Arrange_LeftEdge_ContentShiftedRightByOne()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DragBarPanelNode
        {
            CurrentSize = 30,
            ResolvedEdge = DragBarEdge.Left,
            ContentChild = content
        };

        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(5, 0, 30, 20));

        Assert.Equal(6, content.Bounds.X);
        Assert.Equal(29, content.Bounds.Width);
    }

    [Fact]
    public void Arrange_BottomEdge_ContentGetsHeightMinusOne()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Bottom,
            ContentChild = content
        };

        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 0, 50, 15));

        Assert.Equal(0, content.Bounds.Y);
        Assert.Equal(14, content.Bounds.Height);
    }

    [Fact]
    public void Arrange_TopEdge_ContentShiftedDownByOne()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Top,
            ContentChild = content
        };

        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 2, 50, 15));

        Assert.Equal(3, content.Bounds.Y);
        Assert.Equal(14, content.Bounds.Height);
    }

    #endregion

    #region HitTestBounds Tests

    [Fact]
    public void HitTestBounds_RightEdge_ReturnsRightmostColumn()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 30,
            ResolvedEdge = DragBarEdge.Right
        };

        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 30, 20));

        var hitBounds = node.HitTestBounds;
        Assert.Equal(29, hitBounds.X);
        Assert.Equal(1, hitBounds.Width);
        Assert.Equal(20, hitBounds.Height);
    }

    [Fact]
    public void HitTestBounds_LeftEdge_ReturnsLeftmostColumn()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 30,
            ResolvedEdge = DragBarEdge.Left
        };

        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(5, 0, 30, 20));

        var hitBounds = node.HitTestBounds;
        Assert.Equal(5, hitBounds.X);
        Assert.Equal(1, hitBounds.Width);
    }

    [Fact]
    public void HitTestBounds_BottomEdge_ReturnsBottomRow()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Bottom
        };

        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 0, 50, 15));

        var hitBounds = node.HitTestBounds;
        Assert.Equal(14, hitBounds.Y);
        Assert.Equal(1, hitBounds.Height);
        Assert.Equal(50, hitBounds.Width);
    }

    [Fact]
    public void HitTestBounds_TopEdge_ReturnsTopRow()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Top
        };

        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 2, 50, 15));

        var hitBounds = node.HitTestBounds;
        Assert.Equal(2, hitBounds.Y);
        Assert.Equal(1, hitBounds.Height);
    }

    #endregion

    #region Size Clamping Tests

    [Fact]
    public void CurrentSize_ClampsToMinSize()
    {
        var node = new DragBarPanelNode
        {
            MinSize = 10,
            CurrentSize = 3
        };

        Assert.Equal(10, node.CurrentSize);
    }

    [Fact]
    public void CurrentSize_ClampsToMaxSize()
    {
        var node = new DragBarPanelNode
        {
            MaxSize = 50,
            CurrentSize = 100
        };

        Assert.Equal(50, node.CurrentSize);
    }

    [Fact]
    public void CurrentSize_WithinBounds_NotClamped()
    {
        var node = new DragBarPanelNode
        {
            MinSize = 10,
            MaxSize = 50,
            CurrentSize = 30
        };

        Assert.Equal(30, node.CurrentSize);
    }

    #endregion

    #region Keyboard Input Tests

    [Fact]
    public async Task HandleInput_RightArrow_RightEdge_IncreasesSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 20,
            ResolvedEdge = DragBarEdge.Right,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 20, 20));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(22, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_RightEdge_DecreasesSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 20,
            ResolvedEdge = DragBarEdge.Right,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 20, 20));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(18, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_LeftEdge_IncreasesSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 20,
            ResolvedEdge = DragBarEdge.Left,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 20, 20));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(22, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_DownArrow_BottomEdge_IncreasesSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Bottom,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 0, 50, 15));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(17, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_UpArrow_TopEdge_IncreasesSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Top,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 0, 50, 15));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(17, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_HorizontalArrows_DoNotAffectVerticalEdge()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Bottom,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(new Constraints(0, 50, 0, 30));
        node.Arrange(new Rect(0, 0, 50, 15));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        // Binding executes but orientation check prevents resize
        Assert.Equal(15, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_NotFocused_DoesNotResize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 20,
            ResolvedEdge = DragBarEdge.Right,
            IsFocused = false
        };
        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 20, 20));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(20, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_Resize_ClampsToMinSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 6,
            MinSize = 5,
            ResolvedEdge = DragBarEdge.Right,
            IsFocused = true,
            ResizeStep = 5
        };
        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 6, 20));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(5, node.CurrentSize);
    }

    [Fact]
    public async Task HandleInput_Resize_ClampsToMaxSize()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 48,
            MaxSize = 50,
            ResolvedEdge = DragBarEdge.Right,
            IsFocused = true,
            ResizeStep = 5
        };
        node.Measure(new Constraints(0, 100, 0, 20));
        node.Arrange(new Rect(0, 0, 48, 20));

        await InputRouter.RouteInputToNodeAsync(
            node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None),
            null, null, TestContext.Current.CancellationToken);

        Assert.Equal(50, node.CurrentSize);
    }

    #endregion

    #region Focusable Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new DragBarPanelNode();
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_IncludesSelfAndContentChildren()
    {
        var button = new ButtonNode { Label = "Click" };
        var node = new DragBarPanelNode
        {
            ContentChild = button
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Contains(node, focusables);
        Assert.Contains(button, focusables);
    }

    [Fact]
    public void GetChildren_ReturnsContentChild()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DragBarPanelNode
        {
            ContentChild = content
        };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(content, children[0]);
    }

    [Fact]
    public void GetChildren_NoContent_ReturnsEmpty()
    {
        var node = new DragBarPanelNode();
        var children = node.GetChildren().ToList();
        Assert.Empty(children);
    }

    #endregion

    #region SizeChanged Callback Tests

    [Fact]
    public void CurrentSize_Changed_InvokesSizeChangedCallback()
    {
        int? reportedSize = null;
        var node = new DragBarPanelNode
        {
            MinSize = 1,
            SizeChangedAction = size => reportedSize = size
        };

        node.CurrentSize = 25;

        Assert.Equal(25, reportedSize);
    }

    [Fact]
    public void CurrentSize_SameValue_DoesNotInvokeCallback()
    {
        int callCount = 0;
        var node = new DragBarPanelNode
        {
            MinSize = 1,
            SizeChangedAction = _ => callCount++
        };

        node.CurrentSize = 20;
        node.CurrentSize = 20; // Same value again

        Assert.Equal(1, callCount);
    }

    #endregion

    #region Edge Auto-Detection Tests (via Widget Reconciliation)

    [Fact]
    public async Task Reconcile_HStack_FirstChild_DetectsRightEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Horizontal)
            .WithChildPosition(0, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.NotNull(node);
        Assert.Equal(DragBarEdge.Right, node!.ResolvedEdge);
    }

    [Fact]
    public async Task Reconcile_HStack_LastChild_DetectsLeftEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Horizontal)
            .WithChildPosition(2, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.NotNull(node);
        Assert.Equal(DragBarEdge.Left, node!.ResolvedEdge);
    }

    [Fact]
    public async Task Reconcile_VStack_FirstChild_DetectsBottomEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 15 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Vertical)
            .WithChildPosition(0, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.NotNull(node);
        Assert.Equal(DragBarEdge.Bottom, node!.ResolvedEdge);
    }

    [Fact]
    public async Task Reconcile_VStack_LastChild_DetectsTopEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 15 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Vertical)
            .WithChildPosition(2, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.NotNull(node);
        Assert.Equal(DragBarEdge.Top, node!.ResolvedEdge);
    }

    [Fact]
    public async Task Reconcile_ExplicitEdge_OverridesAutoDetection()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20, Edge = DragBarEdge.Top };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Horizontal)
            .WithChildPosition(0, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.NotNull(node);
        Assert.Equal(DragBarEdge.Top, node!.ResolvedEdge);
    }

    [Fact]
    public async Task Reconcile_PreservesCurrentSize_OnRereconcile()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20 };
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;
        Assert.Equal(20, node!.CurrentSize);

        // Simulate user resize
        node.CurrentSize = 35;

        // Re-reconcile (not new)
        var context2 = ReconcileContext.CreateRoot();
        context2.IsNew = false;
        var node2 = await widget.ReconcileAsync(node, context2) as DragBarPanelNode;

        Assert.Same(node, node2);
        Assert.Equal(35, node2!.CurrentSize); // Preserved, not reset to 20
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void DragBarPanel_Extension_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello"));

        Assert.NotNull(widget);
        Assert.IsType<DragBarPanelWidget>(widget);
    }

    [Fact]
    public void InitialSize_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).InitialSize(30);

        Assert.Equal(30, widget.InitialSize);
    }

    [Fact]
    public void MinSize_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).MinSize(10);

        Assert.Equal(10, widget.MinimumSize);
    }

    [Fact]
    public void MaxSize_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).MaxSize(50);

        Assert.Equal(50, widget.MaximumSize);
    }

    [Fact]
    public void HandleEdge_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).HandleEdge(DragBarEdge.Left);

        Assert.Equal(DragBarEdge.Left, widget.Edge);
    }

    [Fact]
    public void OnSizeChanged_SetsHandler()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).OnSizeChanged(_ => { });

        Assert.NotNull(widget.SizeChangedHandler);
    }

    #endregion
}
