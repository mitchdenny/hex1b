using Hex1b;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for PaddingNode and PaddingWidget.
/// </summary>
public class PaddingNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_AddsPaddingToChildSize()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new PaddingNode { Child = child, Left = 2, Right = 3, Top = 1, Bottom = 1 };

        var size = node.Measure(Constraints.Unbounded);

        // ButtonNode: "[ OK ]" = 6 wide, 1 tall
        // With padding: 6 + 2 + 3 = 11 wide, 1 + 1 + 1 = 3 tall
        Assert.Equal(6 + 5, size.Width);
        Assert.Equal(1 + 2, size.Height);
    }

    [Fact]
    public void Measure_NullChild_ReturnsPaddingOnly()
    {
        var node = new PaddingNode { Child = null, Left = 2, Right = 3, Top = 1, Bottom = 4 };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(5, size.Width);
        Assert.Equal(5, size.Height);
    }

    [Fact]
    public void Measure_ZeroPadding_MatchesChild()
    {
        var child = new ButtonNode { Label = "Test" };
        var node = new PaddingNode { Child = child, Left = 0, Right = 0, Top = 0, Bottom = 0 };

        var childSize = child.Measure(Constraints.Unbounded);
        var paddedSize = node.Measure(Constraints.Unbounded);

        Assert.Equal(childSize, paddedSize);
    }

    [Fact]
    public void Measure_SubtractsPaddingFromConstraints()
    {
        var child = new ButtonNode { Label = "A Very Long Label That Should Be Constrained" };
        var node = new PaddingNode { Child = child, Left = 5, Right = 5, Top = 0, Bottom = 0 };

        var size = node.Measure(new Constraints(0, 20, 0, 10));

        // Max width 20 - 10 padding = 10 for child, then +10 back = capped at 20
        Assert.True(size.Width <= 20);
    }

    [Fact]
    public void Measure_LargePaddingExceedingConstraints_ClampsToZero()
    {
        var child = new ButtonNode { Label = "Hi" };
        var node = new PaddingNode { Child = child, Left = 50, Right = 50, Top = 50, Bottom = 50 };

        // Child constraints should be clamped to 0, not go negative
        var size = node.Measure(new Constraints(0, 10, 0, 10));

        Assert.True(size.Width <= 10);
        Assert.True(size.Height <= 10);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_OffsetsChildByPadding()
    {
        var child = new ButtonNode { Label = "OK" };
        var node = new PaddingNode { Child = child, Left = 3, Right = 2, Top = 1, Bottom = 1 };
        node.Measure(Constraints.Unbounded);

        node.Arrange(new Rect(10, 20, 30, 10));

        Assert.Equal(new Rect(10, 20, 30, 10), node.Bounds);
        Assert.Equal(13, child.Bounds.X);  // 10 + 3
        Assert.Equal(21, child.Bounds.Y);  // 20 + 1
        Assert.Equal(25, child.Bounds.Width);  // 30 - 3 - 2
        Assert.Equal(8, child.Bounds.Height);  // 10 - 1 - 1
    }

    [Fact]
    public void Arrange_NullChild_DoesNotThrow()
    {
        var node = new PaddingNode { Child = null, Left = 1, Right = 1, Top = 1, Bottom = 1 };

        node.Arrange(new Rect(0, 0, 10, 5));

        Assert.Equal(new Rect(0, 0, 10, 5), node.Bounds);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_DelegatesToChild()
    {
        var child = new ButtonNode { Label = "Focusable" };
        var node = new PaddingNode { Child = child, Left = 1, Right = 1, Top = 1, Bottom = 1 };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(child, focusables[0]);
    }

    [Fact]
    public void GetFocusableNodes_NullChild_ReturnsEmpty()
    {
        var node = new PaddingNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    #endregion

    #region GetChildren Tests

    [Fact]
    public void GetChildren_WithChild_ReturnsChild()
    {
        var child = new ButtonNode { Label = "Child" };
        var node = new PaddingNode { Child = child };

        var children = node.GetChildren().ToList();

        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void GetChildren_NullChild_ReturnsEmpty()
    {
        var node = new PaddingNode { Child = null };

        Assert.Empty(node.GetChildren());
    }

    #endregion

    #region Widget Reconciliation Tests

    [Fact]
    public async Task Widget_ReconcileAsync_CreatesNode()
    {
        var widget = new PaddingWidget(2, 3, 1, 1, new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();

        var node = await widget.ReconcileAsync(null, context);

        var padding = Assert.IsType<PaddingNode>(node);
        Assert.Equal(2, padding.Left);
        Assert.Equal(3, padding.Right);
        Assert.Equal(1, padding.Top);
        Assert.Equal(1, padding.Bottom);
        Assert.NotNull(padding.Child);
    }

    [Fact]
    public async Task Widget_ReconcileAsync_ReusesExistingNode()
    {
        var widget = new PaddingWidget(1, 1, 1, 1, new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();
        var existingNode = new PaddingNode();

        var node = await widget.ReconcileAsync(existingNode, context);

        Assert.Same(existingNode, node);
    }

    [Fact]
    public async Task Widget_ReconcileAsync_UpdatesPaddingValues()
    {
        var widget1 = new PaddingWidget(1, 1, 1, 1, new TextBlockWidget("Hello"));
        var context = ReconcileContext.CreateRoot();
        var node = (PaddingNode)await widget1.ReconcileAsync(null, context);

        var widget2 = new PaddingWidget(5, 10, 2, 3, new TextBlockWidget("Hello"));
        await widget2.ReconcileAsync(node, context);

        Assert.Equal(5, node.Left);
        Assert.Equal(10, node.Right);
        Assert.Equal(2, node.Top);
        Assert.Equal(3, node.Bottom);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void Extensions_Padding_FourSides_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Padding(1, 2, 3, 4, new TextBlockWidget("Hello"));

        Assert.Equal(1, widget.Left);
        Assert.Equal(2, widget.Right);
        Assert.Equal(3, widget.Top);
        Assert.Equal(4, widget.Bottom);
    }

    [Fact]
    public void Extensions_Padding_Uniform_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Padding(3, new TextBlockWidget("Hello"));

        Assert.Equal(3, widget.Left);
        Assert.Equal(3, widget.Right);
        Assert.Equal(3, widget.Top);
        Assert.Equal(3, widget.Bottom);
    }

    [Fact]
    public void Extensions_Padding_HorizontalVertical_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Padding(2, 1, new TextBlockWidget("Hello"));

        Assert.Equal(2, widget.Left);
        Assert.Equal(2, widget.Right);
        Assert.Equal(1, widget.Top);
        Assert.Equal(1, widget.Bottom);
    }

    [Fact]
    public void Extensions_Padding_Builder_CreatesWidget()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Padding(1, 1, 0, 0, p => p.Text("Indented"));

        Assert.IsType<PaddingWidget>(widget);
    }

    [Fact]
    public void Extensions_Padding_ArrayBuilder_CreatesVStack()
    {
        var ctx = new WidgetContext<VStackWidget>();
        var widget = ctx.Padding(1, 1, 0, 0, p => new Hex1bWidget[]
        {
            p.Text("Line 1"),
            p.Text("Line 2"),
        });

        Assert.IsType<PaddingWidget>(widget);
    }

    #endregion
}
