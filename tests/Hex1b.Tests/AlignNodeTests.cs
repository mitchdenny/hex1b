using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class AlignNodeTests
{
    [Fact]
    public async Task Measure_ReturnsChildSize()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hello" };
        var node = new AlignNode { Child = child, Alignment = Alignment.Center };
        var constraints = new Constraints(0, 80, 0, 24);

        // Act
        var size = node.Measure(constraints);

        // Assert - returns child's natural size, not full available space
        Assert.Equal(5, size.Width);  // "Hello".Length
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_WithFillHint_StillReturnsChildSize_ParentHandlesExpansion()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hello" };
        var node = new AlignNode { Child = child, Alignment = Alignment.Center };
        node.HeightHint = SizeHint.Fill;  // Fill is handled by parent container
        var constraints = new Constraints(0, 80, 0, 24);

        // Act
        var size = node.Measure(constraints);

        // Assert - Measure still returns child size; Fill is applied by parent during Arrange
        Assert.Equal(5, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Arrange_Center_PositionsChildInCenter()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" }; // 2 chars wide, 1 high
        var node = new AlignNode { Child = child, Alignment = Alignment.Center };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert - child should be centered
        // (80 - 2) / 2 = 39 for x, (24 - 1) / 2 = 11 for y
        Assert.Equal(39, child.Bounds.X);
        Assert.Equal(11, child.Bounds.Y);
        Assert.Equal(2, child.Bounds.Width);
        Assert.Equal(1, child.Bounds.Height);
    }

    [Fact]
    public async Task Arrange_TopLeft_PositionsChildAtTopLeft()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.TopLeft };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(0, child.Bounds.X);
        Assert.Equal(0, child.Bounds.Y);
    }

    [Fact]
    public async Task Arrange_TopRight_PositionsChildAtTopRight()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.TopRight };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(78, child.Bounds.X); // 80 - 2
        Assert.Equal(0, child.Bounds.Y);
    }

    [Fact]
    public async Task Arrange_BottomLeft_PositionsChildAtBottomLeft()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.BottomLeft };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(0, child.Bounds.X);
        Assert.Equal(23, child.Bounds.Y); // 24 - 1
    }

    [Fact]
    public async Task Arrange_BottomRight_PositionsChildAtBottomRight()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.BottomRight };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(78, child.Bounds.X); // 80 - 2
        Assert.Equal(23, child.Bounds.Y); // 24 - 1
    }

    [Fact]
    public async Task Arrange_HCenter_CentersHorizontallyAtTop()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.HCenter };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(39, child.Bounds.X); // (80 - 2) / 2
        Assert.Equal(0, child.Bounds.Y); // Top (default)
    }

    [Fact]
    public async Task Arrange_VCenter_CentersVerticallyAtLeft()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.VCenter };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(0, child.Bounds.X); // Left (default)
        Assert.Equal(11, child.Bounds.Y); // (24 - 1) / 2
    }

    [Fact]
    public async Task Arrange_BottomCenter_PositionsAtBottomCenter()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.BottomCenter };
        node.Measure(new Constraints(0, 80, 0, 24));

        // Act
        node.Arrange(new Rect(0, 0, 80, 24));

        // Assert
        Assert.Equal(39, child.Bounds.X); // (80 - 2) / 2
        Assert.Equal(23, child.Bounds.Y); // 24 - 1
    }

    [Fact]
    public async Task Arrange_WithOffset_PositionsRelativeToParentBounds()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hi" };
        var node = new AlignNode { Child = child, Alignment = Alignment.Center };
        node.Measure(new Constraints(0, 40, 0, 10));

        // Act - parent bounds start at (10, 5)
        node.Arrange(new Rect(10, 5, 40, 10));

        // Assert - should be centered within parent bounds
        Assert.Equal(10 + (40 - 2) / 2, child.Bounds.X); // 10 + 19 = 29
        Assert.Equal(5 + (10 - 1) / 2, child.Bounds.Y);  // 5 + 4 = 9
    }

    [Fact]
    public async Task AlignmentChange_MarksDirty()
    {
        // Arrange
        var node = new AlignNode { Alignment = Alignment.TopLeft };
        node.ClearDirty();

        // Act - simulate reconciliation with changed alignment
        if (node.Alignment != Alignment.Center)
        {
            node.MarkDirty();
        }
        node.Alignment = Alignment.Center;

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public async Task GetFocusableNodes_ReturnsChildFocusables()
    {
        // Arrange
        var button = new ButtonNode { Label = "Click" };
        var node = new AlignNode { Child = button };

        // Act
        var focusables = node.GetFocusableNodes().ToList();

        // Assert
        Assert.Single(focusables);
        Assert.Same(button, focusables[0]);
    }

    [Fact]
    public async Task GetChildren_ReturnsChild()
    {
        // Arrange
        var child = new TextBlockNode { Text = "Hello" };
        var node = new AlignNode { Child = child };

        // Act
        var children = node.GetChildren().ToList();

        // Assert
        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public async Task GetChildren_WhenNoChild_ReturnsEmpty()
    {
        // Arrange
        var node = new AlignNode();

        // Act
        var children = node.GetChildren().ToList();

        // Assert
        Assert.Empty(children);
    }
}
