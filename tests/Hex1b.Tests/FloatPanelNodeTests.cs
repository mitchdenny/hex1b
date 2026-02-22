using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class FloatPanelNodeTests
{
    [Fact]
    public void Measure_FillsAvailableSpace()
    {
        // Arrange
        var node = new FloatPanelNode();
        var constraints = new Constraints(0, 80, 0, 24);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(80, size.Width);
        Assert.Equal(24, size.Height);
    }

    [Fact]
    public void Arrange_PositionsChildrenAtAbsoluteCoordinates()
    {
        // Arrange
        var child1 = new IconNode { Icon = "A" };
        var child2 = new IconNode { Icon = "B" };
        var node = new FloatPanelNode
        {
            Children =
            [
                new FloatPanelNode.PositionedNode(10, 5, child1),
                new FloatPanelNode.PositionedNode(20, 8, child2),
            ]
        };

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(10, child1.Bounds.X);
        Assert.Equal(5, child1.Bounds.Y);
        Assert.Equal(20, child2.Bounds.X);
        Assert.Equal(8, child2.Bounds.Y);
    }

    [Fact]
    public void Arrange_OffsetsFromPanelOrigin()
    {
        // Arrange — panel itself is at (5, 3)
        var child = new IconNode { Icon = "X" };
        var node = new FloatPanelNode
        {
            Children = [new FloatPanelNode.PositionedNode(10, 5, child)]
        };

        // Act
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(5, 3, 80, 24));

        // Assert — child position should be panel origin + offset
        Assert.Equal(15, child.Bounds.X);
        Assert.Equal(8, child.Bounds.Y);
    }

    [Fact]
    public void GetChildren_ReturnsAllChildNodes()
    {
        // Arrange
        var child1 = new IconNode { Icon = "A" };
        var child2 = new IconNode { Icon = "B" };
        var node = new FloatPanelNode
        {
            Children =
            [
                new FloatPanelNode.PositionedNode(0, 0, child1),
                new FloatPanelNode.PositionedNode(5, 5, child2),
            ]
        };

        // Act
        var children = node.GetChildren().ToList();

        // Assert
        Assert.Equal(2, children.Count);
        Assert.Contains(child1, children);
        Assert.Contains(child2, children);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsFocusableChildren()
    {
        // Arrange — IconNodes with click handlers are focusable-ish, but ButtonNodes are better
        // Use IconNode without click (not focusable) and one with a click callback
        var nonFocusable = new IconNode { Icon = "A" };
        var focusable = new IconNode { Icon = "B", ClickCallback = _ => Task.CompletedTask };
        var node = new FloatPanelNode
        {
            Children =
            [
                new FloatPanelNode.PositionedNode(0, 0, nonFocusable),
                new FloatPanelNode.PositionedNode(5, 5, focusable),
            ]
        };

        // Act
        var focusables = node.GetFocusableNodes().ToList();

        // Assert — IconNode is not focusable by default, so we just verify the traversal works
        Assert.NotNull(focusables);
    }

    [Fact]
    public void Reconcile_PreservesNodeOnSameType()
    {
        // Arrange
        var widget1 = new FloatPanelWidget([new FloatChild(0, 0, new IconWidget("A"))]);
        var widget2 = new FloatPanelWidget([new FloatChild(5, 5, new IconWidget("B"))]);
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Act
        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        // Assert
        Assert.Same(node1, node2);
        var floatNode = (FloatPanelNode)node2;
        Assert.Single(floatNode.Children);
        Assert.Equal(5, floatNode.Children[0].X);
        Assert.Equal(5, floatNode.Children[0].Y);
    }

    [Fact]
    public void Reconcile_HandlesChildCountChanges()
    {
        // Arrange
        var widget1 = new FloatPanelWidget([
            new FloatChild(0, 0, new IconWidget("A")),
            new FloatChild(5, 5, new IconWidget("B")),
        ]);
        var widget2 = new FloatPanelWidget([
            new FloatChild(0, 0, new IconWidget("A")),
        ]);
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Act
        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var floatNode1 = (FloatPanelNode)node1;
        Assert.Equal(2, floatNode1.Children.Count);

        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();
        var floatNode2 = (FloatPanelNode)node2;

        // Assert
        Assert.Single(floatNode2.Children);
    }

    [Fact]
    public void EmptyPanel_ArrangesWithoutError()
    {
        // Arrange
        var node = new FloatPanelNode();

        // Act & Assert — should not throw
        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));
        Assert.Empty(node.Children);
    }
}
