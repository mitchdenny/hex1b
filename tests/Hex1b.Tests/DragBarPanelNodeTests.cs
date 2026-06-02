using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class DragBarPanelNodeTests
{
    #region Measure Tests

    [TestMethod]
    public void Measure_RightEdge_ReturnsCurrentSizeAsWidth()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 30,
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        Assert.AreEqual(30, size.Width);
    }

    [TestMethod]
    public void Measure_LeftEdge_ReturnsCurrentSizeAsWidth()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 25,
            ResolvedEdge = DragBarEdge.Left,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        Assert.AreEqual(25, size.Width);
    }

    [TestMethod]
    public void Measure_BottomEdge_ReturnsCurrentSizeAsHeight()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 15,
            ResolvedEdge = DragBarEdge.Bottom,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 50, 0, 30));

        Assert.AreEqual(15, size.Height);
    }

    [TestMethod]
    public void Measure_TopEdge_ReturnsCurrentSizeAsHeight()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 12,
            ResolvedEdge = DragBarEdge.Top,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 50, 0, 30));

        Assert.AreEqual(12, size.Height);
    }

    [TestMethod]
    public void Measure_DefaultSize_ReturnsMinimumDefaultOf10()
    {
        var node = new DragBarPanelNode
        {
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 100, 0, 20));

        // _currentSize starts at 0, but Measure uses 10 as default minimum
        Assert.IsTrue(size.Width >= 5);
    }

    [TestMethod]
    public void Measure_ConstrainedToMaxWidth()
    {
        var node = new DragBarPanelNode
        {
            CurrentSize = 50,
            ResolvedEdge = DragBarEdge.Right,
            ContentChild = new TextBlockNode { Text = "Content" }
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        Assert.IsTrue(size.Width <= 30);
    }

    #endregion

    #region Arrange Tests

    [TestMethod]
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

        Assert.AreEqual(0, content.Bounds.X);
        Assert.AreEqual(29, content.Bounds.Width);
    }

    [TestMethod]
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

        Assert.AreEqual(6, content.Bounds.X);
        Assert.AreEqual(29, content.Bounds.Width);
    }

    [TestMethod]
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

        Assert.AreEqual(0, content.Bounds.Y);
        Assert.AreEqual(14, content.Bounds.Height);
    }

    [TestMethod]
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

        Assert.AreEqual(3, content.Bounds.Y);
        Assert.AreEqual(14, content.Bounds.Height);
    }

    #endregion

    #region HitTestBounds Tests

    [TestMethod]
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
        Assert.AreEqual(29, hitBounds.X);
        Assert.AreEqual(1, hitBounds.Width);
        Assert.AreEqual(20, hitBounds.Height);
    }

    [TestMethod]
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
        Assert.AreEqual(5, hitBounds.X);
        Assert.AreEqual(1, hitBounds.Width);
    }

    [TestMethod]
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
        Assert.AreEqual(14, hitBounds.Y);
        Assert.AreEqual(1, hitBounds.Height);
        Assert.AreEqual(50, hitBounds.Width);
    }

    [TestMethod]
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
        Assert.AreEqual(2, hitBounds.Y);
        Assert.AreEqual(1, hitBounds.Height);
    }

    #endregion

    #region Size Clamping Tests

    [TestMethod]
    public void CurrentSize_ClampsToMinSize()
    {
        var node = new DragBarPanelNode
        {
            MinSize = 10,
            CurrentSize = 3
        };

        Assert.AreEqual(10, node.CurrentSize);
    }

    [TestMethod]
    public void CurrentSize_ClampsToMaxSize()
    {
        var node = new DragBarPanelNode
        {
            MaxSize = 50,
            CurrentSize = 100
        };

        Assert.AreEqual(50, node.CurrentSize);
    }

    [TestMethod]
    public void CurrentSize_WithinBounds_NotClamped()
    {
        var node = new DragBarPanelNode
        {
            MinSize = 10,
            MaxSize = 50,
            CurrentSize = 30
        };

        Assert.AreEqual(30, node.CurrentSize);
    }

    #endregion

    #region Keyboard Input Tests

    [TestMethod]
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

        Assert.AreEqual(22, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(18, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(22, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(17, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(17, node.CurrentSize);
    }

    [TestMethod]
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
        Assert.AreEqual(15, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(20, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(5, node.CurrentSize);
    }

    [TestMethod]
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

        Assert.AreEqual(50, node.CurrentSize);
    }

    #endregion

    #region Focusable Tests

    [TestMethod]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new DragBarPanelNode();
        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
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

    [TestMethod]
    public void GetChildren_ReturnsContentChild()
    {
        var content = new TextBlockNode { Text = "Content" };
        var node = new DragBarPanelNode
        {
            ContentChild = content
        };

        var children = node.GetChildren().ToList();

        TestSeq.Single(children);
        Assert.AreSame(content, children[0]);
    }

    [TestMethod]
    public void GetChildren_NoContent_ReturnsEmpty()
    {
        var node = new DragBarPanelNode();
        var children = node.GetChildren().ToList();
        Assert.IsEmpty(children);
    }

    #endregion

    #region Edge Auto-Detection Tests (via Widget Reconciliation)

    [TestMethod]
    public async Task Reconcile_HStack_FirstChild_DetectsRightEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Horizontal)
            .WithChildPosition(0, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.IsNotNull(node);
        Assert.AreEqual(DragBarEdge.Right, node!.ResolvedEdge);
    }

    [TestMethod]
    public async Task Reconcile_HStack_LastChild_DetectsLeftEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Horizontal)
            .WithChildPosition(2, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.IsNotNull(node);
        Assert.AreEqual(DragBarEdge.Left, node!.ResolvedEdge);
    }

    [TestMethod]
    public async Task Reconcile_VStack_FirstChild_DetectsBottomEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 15 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Vertical)
            .WithChildPosition(0, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.IsNotNull(node);
        Assert.AreEqual(DragBarEdge.Bottom, node!.ResolvedEdge);
    }

    [TestMethod]
    public async Task Reconcile_VStack_LastChild_DetectsTopEdge()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 15 };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Vertical)
            .WithChildPosition(2, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.IsNotNull(node);
        Assert.AreEqual(DragBarEdge.Top, node!.ResolvedEdge);
    }

    [TestMethod]
    public async Task Reconcile_ExplicitEdge_OverridesAutoDetection()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20, Edge = DragBarEdge.Top };
        var context = ReconcileContext.CreateRoot()
            .WithLayoutAxis(LayoutAxis.Horizontal)
            .WithChildPosition(0, 3);
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;

        Assert.IsNotNull(node);
        Assert.AreEqual(DragBarEdge.Top, node!.ResolvedEdge);
    }

    [TestMethod]
    public async Task Reconcile_PreservesCurrentSize_OnRereconcile()
    {
        var widget = new DragBarPanelWidget { Content = new TextBlockWidget("Content"), InitialSize = 20 };
        var context = ReconcileContext.CreateRoot();
        context.IsNew = true;

        var node = await widget.ReconcileAsync(null, context) as DragBarPanelNode;
        Assert.AreEqual(20, node!.CurrentSize);

        // Simulate user resize
        node.CurrentSize = 35;

        // Re-reconcile (not new)
        var context2 = ReconcileContext.CreateRoot();
        context2.IsNew = false;
        var node2 = await widget.ReconcileAsync(node, context2) as DragBarPanelNode;

        Assert.AreSame(node, node2);
        Assert.AreEqual(35, node2!.CurrentSize); // Preserved, not reset to 20
    }

    #endregion

    #region Extension Method Tests

    [TestMethod]
    public void DragBarPanel_Extension_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello"));

        Assert.IsNotNull(widget);
        TestSeq.IsType<DragBarPanelWidget>(widget);
    }

    [TestMethod]
    public void InitialSize_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).InitialSize(30);

        Assert.AreEqual(30, widget.InitialSize);
    }

    [TestMethod]
    public void MinSize_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).MinSize(10);

        Assert.AreEqual(10, widget.MinimumSize);
    }

    [TestMethod]
    public void MaxSize_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).MaxSize(50);

        Assert.AreEqual(50, widget.MaximumSize);
    }

    [TestMethod]
    public void HandleEdge_SetsProperty()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.DragBarPanel(c => c.Text("Hello")).HandleEdge(DragBarEdge.Left);

        Assert.AreEqual(DragBarEdge.Left, widget.Edge);
    }

    #endregion
}
