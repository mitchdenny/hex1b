using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for ScrollbarNode and ScrollbarWidget.
/// </summary>
[TestClass]
public class ScrollbarNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    #region Basic State Tests

    [TestMethod]
    public void ScrollbarNode_InitialState_HasDefaultValues()
    {
        var node = new ScrollbarNode();

        Assert.AreEqual(ScrollOrientation.Vertical, node.Orientation);
        Assert.AreEqual(100, node.ContentSize);
        Assert.AreEqual(50, node.ViewportSize);
        Assert.AreEqual(0, node.Offset);
        Assert.IsTrue(node.IsScrollable);
        Assert.AreEqual(50, node.MaxOffset);
    }

    [TestMethod]
    public void ScrollbarNode_IsScrollable_FalseWhenViewportExceedsContent()
    {
        var node = new ScrollbarNode
        {
            ContentSize = 10,
            ViewportSize = 20
        };

        Assert.IsFalse(node.IsScrollable);
        Assert.AreEqual(0, node.MaxOffset);
    }

    [TestMethod]
    public void ScrollbarNode_IsFocusable_OnlyWhenScrollable()
    {
        var scrollable = new ScrollbarNode { ContentSize = 100, ViewportSize = 50 };
        var notScrollable = new ScrollbarNode { ContentSize = 10, ViewportSize = 50 };

        Assert.IsTrue(scrollable.IsFocusable);
        Assert.IsFalse(notScrollable.IsFocusable);
    }

    #endregion

    #region Measure Tests

    [TestMethod]
    public void Measure_VerticalScrollbar_Returns1Width()
    {
        var node = new ScrollbarNode { Orientation = ScrollOrientation.Vertical };

        var size = node.Measure(new Constraints(0, 100, 0, 50));

        Assert.AreEqual(1, size.Width);
        Assert.AreEqual(50, size.Height);
    }

    [TestMethod]
    public void Measure_HorizontalScrollbar_Returns1Height()
    {
        var node = new ScrollbarNode { Orientation = ScrollOrientation.Horizontal };

        var size = node.Measure(new Constraints(0, 50, 0, 100));

        Assert.AreEqual(50, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    #endregion

    #region Widget Reconciliation Tests

    [TestMethod]
    public async Task Widget_Reconcile_CreatesScrollbarNode()
    {
        var widget = new ScrollbarWidget(ScrollOrientation.Vertical, 200, 50, 25);
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        TestSeq.IsType<ScrollbarNode>(node);
        var scrollbar = (ScrollbarNode)node;
        Assert.AreEqual(ScrollOrientation.Vertical, scrollbar.Orientation);
        Assert.AreEqual(200, scrollbar.ContentSize);
        Assert.AreEqual(50, scrollbar.ViewportSize);
        Assert.AreEqual(25, scrollbar.Offset);
    }

    [TestMethod]
    public async Task Widget_Reconcile_UpdatesExistingNode()
    {
        var widget = new ScrollbarWidget(ScrollOrientation.Horizontal, 300, 100, 50);
        var context = ReconcileContext.CreateRoot();
        var existing = new ScrollbarNode();

        var node = await widget.ReconcileAsync(existing, context);

        Assert.AreSame(existing, node);
        Assert.AreEqual(ScrollOrientation.Horizontal, existing.Orientation);
        Assert.AreEqual(300, existing.ContentSize);
        Assert.AreEqual(100, existing.ViewportSize);
        Assert.AreEqual(50, existing.Offset);
    }

    [TestMethod]
    public async Task Widget_OnScroll_HandlerIsCalled()
    {
        var receivedOffset = -1;
        var widget = new ScrollbarWidget(ScrollOrientation.Vertical, 100, 50, 0)
            .OnScroll(offset => receivedOffset = offset);
        var context = ReconcileContext.CreateRoot();

        var node = (ScrollbarNode)await widget.ReconcileAsync(null, context);
        
        Assert.IsNotNull(node.ScrollHandler);
        await node.ScrollHandler(25);
        
        Assert.AreEqual(25, receivedOffset);
    }

    #endregion

    #region Extension Method Tests

    [TestMethod]
    public void VScrollbar_CreatesVerticalScrollbar()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.VScrollbar(200, 50, 10);

        Assert.AreEqual(ScrollOrientation.Vertical, widget.Orientation);
        Assert.AreEqual(200, widget.ContentSize);
        Assert.AreEqual(50, widget.ViewportSize);
        Assert.AreEqual(10, widget.Offset);
    }

    [TestMethod]
    public void HScrollbar_CreatesHorizontalScrollbar()
    {
        var ctx = new WidgetContext<HStackWidget>();
        var widget = ctx.HScrollbar(300, 100, 25);

        Assert.AreEqual(ScrollOrientation.Horizontal, widget.Orientation);
        Assert.AreEqual(300, widget.ContentSize);
        Assert.AreEqual(100, widget.ViewportSize);
        Assert.AreEqual(25, widget.Offset);
    }

    #endregion

    #region Render Tests

    [TestMethod]
    public void Render_VerticalScrollbar_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        var context = CreateContext(workload);

        var node = new ScrollbarNode
        {
            Orientation = ScrollOrientation.Vertical,
            ContentSize = 100,
            ViewportSize = 50,
            Offset = 0
        };

        node.Measure(new Constraints(0, 1, 0, 10));
        node.Arrange(new Rect(0, 0, 1, 10));
        
        // Should not throw
        var ex = TestSeq.RecordException(() => node.Render(context));
        Assert.IsNull(ex);
    }

    [TestMethod]
    public void Render_HorizontalScrollbar_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        var context = CreateContext(workload);

        var node = new ScrollbarNode
        {
            Orientation = ScrollOrientation.Horizontal,
            ContentSize = 100,
            ViewportSize = 50,
            Offset = 0
        };

        node.Measure(new Constraints(0, 10, 0, 1));
        node.Arrange(new Rect(0, 0, 10, 1));
        
        // Should not throw
        var ex = TestSeq.RecordException(() => node.Render(context));
        Assert.IsNull(ex);
    }

    [TestMethod]
    public void Render_NotScrollable_DoesNotThrow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        var context = CreateContext(workload);

        var node = new ScrollbarNode
        {
            ContentSize = 10,
            ViewportSize = 50 // Not scrollable
        };

        node.Measure(new Constraints(0, 1, 0, 10));
        node.Arrange(new Rect(0, 0, 1, 10));
        
        // Should not throw even when not scrollable
        var ex = TestSeq.RecordException(() => node.Render(context));
        Assert.IsNull(ex);
    }

    #endregion
}
