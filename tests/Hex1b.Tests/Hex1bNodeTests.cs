using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Tests for Hex1bNode base class behavior.
/// </summary>
public class Hex1bNodeTests
{
    /// <summary>
    /// Simple concrete node for testing base class behavior.
    /// </summary>
    private sealed class TestNode : Hex1bNode
    {
        public override Size Measure(Constraints constraints) => new(10, 1);
        public override void Render(Hex1bRenderContext context) { }
    }

    #region PreviousBounds Tracking Tests

    [Fact]
    public void Arrange_FirstCall_PreviousBoundsIsEmpty()
    {
        var node = new TestNode();
        var bounds = new Rect(5, 10, 20, 5);

        node.Arrange(bounds);

        // Before first arrange, PreviousBounds should be the default empty rect
        Assert.Equal(new Rect(0, 0, 0, 0), node.PreviousBounds);
        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Arrange_SecondCall_PreviousBoundsHasFirstBounds()
    {
        var node = new TestNode();
        var firstBounds = new Rect(5, 10, 20, 5);
        var secondBounds = new Rect(0, 0, 30, 10);

        node.Arrange(firstBounds);
        node.Arrange(secondBounds);

        Assert.Equal(firstBounds, node.PreviousBounds);
        Assert.Equal(secondBounds, node.Bounds);
    }

    [Fact]
    public void Arrange_MultipleCallsTracksPreviousBounds()
    {
        var node = new TestNode();
        var bounds1 = new Rect(0, 0, 10, 5);
        var bounds2 = new Rect(5, 5, 15, 8);
        var bounds3 = new Rect(10, 10, 20, 10);

        node.Arrange(bounds1);
        Assert.Equal(new Rect(0, 0, 0, 0), node.PreviousBounds);
        Assert.Equal(bounds1, node.Bounds);

        node.Arrange(bounds2);
        Assert.Equal(bounds1, node.PreviousBounds);
        Assert.Equal(bounds2, node.Bounds);

        node.Arrange(bounds3);
        Assert.Equal(bounds2, node.PreviousBounds);
        Assert.Equal(bounds3, node.Bounds);
    }

    [Fact]
    public void Arrange_SameBounds_PreviousBoundsEqualsCurrentBounds()
    {
        var node = new TestNode();
        var bounds = new Rect(5, 10, 20, 5);

        node.Arrange(bounds);
        node.Arrange(bounds);

        // When arranged with the same bounds, previous equals current
        Assert.Equal(bounds, node.PreviousBounds);
        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void BoundsDidMove_WhenPositionChanges_ReturnsTrue()
    {
        var node = new TestNode();
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Arrange(new Rect(5, 0, 10, 5)); // X changed

        var moved = node.Bounds.X != node.PreviousBounds.X || 
                    node.Bounds.Y != node.PreviousBounds.Y;
        
        Assert.True(moved);
    }

    [Fact]
    public void BoundsDidResize_WhenSizeChanges_ReturnsTrue()
    {
        var node = new TestNode();
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Arrange(new Rect(0, 0, 20, 5)); // Width changed

        var resized = node.Bounds.Width != node.PreviousBounds.Width || 
                      node.Bounds.Height != node.PreviousBounds.Height;
        
        Assert.True(resized);
    }

    #endregion
}
