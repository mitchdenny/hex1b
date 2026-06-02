using Hex1b.Layout;

namespace Hex1b.Tests;

/// <summary>
/// Tests for Hex1bNode base class behavior.
/// </summary>
[TestClass]
public class Hex1bNodeTests
{
    /// <summary>
    /// Simple concrete node for testing base class behavior.
    /// </summary>
    private sealed class TestNode : Hex1bNode
    {
        protected override Size MeasureCore(Constraints constraints) => new(10, 1);
        public override void Render(Hex1bRenderContext context) { }
    }

    #region PreviousBounds Tracking Tests

    [TestMethod]
    public void Arrange_FirstCall_PreviousBoundsIsEmpty()
    {
        var node = new TestNode();
        var bounds = new Rect(5, 10, 20, 5);

        node.Arrange(bounds);

        // Before first arrange, PreviousBounds should be the default empty rect
        Assert.AreEqual(new Rect(0, 0, 0, 0), node.PreviousBounds);
        Assert.AreEqual(bounds, node.Bounds);
    }

    [TestMethod]
    public void Arrange_SecondCall_PreviousBoundsHasFirstBounds()
    {
        var node = new TestNode();
        var firstBounds = new Rect(5, 10, 20, 5);
        var secondBounds = new Rect(0, 0, 30, 10);

        node.Arrange(firstBounds);
        node.Arrange(secondBounds);

        Assert.AreEqual(firstBounds, node.PreviousBounds);
        Assert.AreEqual(secondBounds, node.Bounds);
    }

    [TestMethod]
    public void Arrange_MultipleCallsTracksPreviousBounds()
    {
        var node = new TestNode();
        var bounds1 = new Rect(0, 0, 10, 5);
        var bounds2 = new Rect(5, 5, 15, 8);
        var bounds3 = new Rect(10, 10, 20, 10);

        node.Arrange(bounds1);
        Assert.AreEqual(new Rect(0, 0, 0, 0), node.PreviousBounds);
        Assert.AreEqual(bounds1, node.Bounds);

        node.Arrange(bounds2);
        Assert.AreEqual(bounds1, node.PreviousBounds);
        Assert.AreEqual(bounds2, node.Bounds);

        node.Arrange(bounds3);
        Assert.AreEqual(bounds2, node.PreviousBounds);
        Assert.AreEqual(bounds3, node.Bounds);
    }

    [TestMethod]
    public void Arrange_SameBounds_PreviousBoundsEqualsCurrentBounds()
    {
        var node = new TestNode();
        var bounds = new Rect(5, 10, 20, 5);

        node.Arrange(bounds);
        node.Arrange(bounds);

        // When arranged with the same bounds, previous equals current
        Assert.AreEqual(bounds, node.PreviousBounds);
        Assert.AreEqual(bounds, node.Bounds);
    }

    [TestMethod]
    public void InheritBoundsFromReplacedNode_SetsBoundsFromOldNode()
    {
        // Arrange - old node with established bounds
        var oldNode = new TestNode();
        var oldBounds = new Rect(5, 10, 30, 15);
        oldNode.Arrange(oldBounds);
        
        // New replacement node starts with empty bounds
        var newNode = new TestNode();
        Assert.AreEqual(new Rect(0, 0, 0, 0), newNode.Bounds);

        // Act - inherit bounds from the replaced node
        newNode.InheritBoundsFromReplacedNode(oldNode);

        // Assert - Bounds is now the old node's bounds
        // When Arrange is called, this will become PreviousBounds
        Assert.AreEqual(oldBounds, newNode.Bounds);
    }

    [TestMethod]
    public void InheritBoundsFromReplacedNode_AllowsDirtyRegionClearing()
    {
        // This tests the scenario where a node is replaced with a smaller one
        // The ClearDirtyRegions logic needs PreviousBounds to know what to clear
        
        // Arrange - old node occupied a large area
        var oldNode = new TestNode();
        var oldBounds = new Rect(0, 0, 40, 10);
        oldNode.Arrange(oldBounds);
        
        // New node is smaller
        var newNode = new TestNode();
        var newBounds = new Rect(0, 0, 20, 5);
        
        // Act - inherit bounds, then arrange at new smaller size
        newNode.InheritBoundsFromReplacedNode(oldNode);
        newNode.Arrange(newBounds);

        // Assert - PreviousBounds should be the inherited old bounds, not empty
        // This allows ClearDirtyRegions to know the full area that was occupied
        Assert.AreEqual(oldBounds, newNode.PreviousBounds);
        Assert.AreEqual(newBounds, newNode.Bounds);
        
        // The difference between these rects is what needs to be cleared
        Assert.IsTrue(newNode.Bounds != newNode.PreviousBounds);
    }

    [TestMethod]
    public void BoundsDidMove_WhenPositionChanges_ReturnsTrue()
    {
        var node = new TestNode();
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Arrange(new Rect(5, 0, 10, 5)); // X changed

        var moved = node.Bounds.X != node.PreviousBounds.X || 
                    node.Bounds.Y != node.PreviousBounds.Y;
        
        Assert.IsTrue(moved);
    }

    [TestMethod]
    public void BoundsDidResize_WhenSizeChanges_ReturnsTrue()
    {
        var node = new TestNode();
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Arrange(new Rect(0, 0, 20, 5)); // Width changed

        var resized = node.Bounds.Width != node.PreviousBounds.Width || 
                      node.Bounds.Height != node.PreviousBounds.Height;
        
        Assert.IsTrue(resized);
    }

    #endregion

    #region Orphaned Child Bounds Tests

    [TestMethod]
    public void AddOrphanedChildBounds_AddsToList()
    {
        var node = new TestNode();
        var orphanBounds = new Rect(0, 5, 10, 1);
        
        node.AddOrphanedChildBounds(orphanBounds);
        
        Assert.IsNotNull(node.OrphanedChildBounds);
        TestSeq.Single(node.OrphanedChildBounds);
        Assert.AreEqual(orphanBounds, node.OrphanedChildBounds[0]);
    }

    [TestMethod]
    public void AddOrphanedChildBounds_MarksDirty()
    {
        var node = new TestNode();
        node.ClearDirty(); // Start clean
        Assert.IsFalse(node.IsDirty);
        
        node.AddOrphanedChildBounds(new Rect(0, 5, 10, 1));
        
        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void AddOrphanedChildBounds_IgnoresEmptyBounds()
    {
        var node = new TestNode();
        
        node.AddOrphanedChildBounds(new Rect(0, 0, 0, 0));
        node.AddOrphanedChildBounds(new Rect(5, 5, 0, 1));
        node.AddOrphanedChildBounds(new Rect(5, 5, 10, 0));
        
        Assert.IsNull(node.OrphanedChildBounds);
    }

    [TestMethod]
    public void ClearOrphanedChildBounds_ClearsList()
    {
        var node = new TestNode();
        node.AddOrphanedChildBounds(new Rect(0, 5, 10, 1));
        node.AddOrphanedChildBounds(new Rect(0, 6, 10, 1));
        Assert.AreEqual(2, node.OrphanedChildBounds!.Count);
        
        node.ClearOrphanedChildBounds();
        
        Assert.IsEmpty(node.OrphanedChildBounds);
    }

    #endregion

    #region Dirty Flag Tests

    [TestMethod]
    public void NewNode_IsDirty()
    {
        var node = new TestNode();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void ClearDirty_SetsIsDirtyToFalse()
    {
        var node = new TestNode();
        Assert.IsTrue(node.IsDirty); // New node starts dirty

        node.ClearDirty();

        Assert.IsFalse(node.IsDirty);
    }

    [TestMethod]
    public void MarkDirty_SetsIsDirtyToTrue()
    {
        var node = new TestNode();
        node.ClearDirty();
        Assert.IsFalse(node.IsDirty);

        node.MarkDirty();

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void Arrange_WithDifferentBounds_MarksDirty()
    {
        var node = new TestNode();
        node.Arrange(new Rect(0, 0, 10, 5));
        node.ClearDirty();
        Assert.IsFalse(node.IsDirty);

        node.Arrange(new Rect(5, 5, 10, 5)); // Different position

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void Arrange_WithSameBounds_DoesNotMarkDirty()
    {
        var node = new TestNode();
        var bounds = new Rect(0, 0, 10, 5);
        node.Arrange(bounds);
        node.ClearDirty();
        Assert.IsFalse(node.IsDirty);

        node.Arrange(bounds); // Same bounds

        Assert.IsFalse(node.IsDirty);
    }

    [TestMethod]
    public void Arrange_WithDifferentSize_MarksDirty()
    {
        var node = new TestNode();
        node.Arrange(new Rect(0, 0, 10, 5));
        node.ClearDirty();

        node.Arrange(new Rect(0, 0, 20, 10)); // Different size

        Assert.IsTrue(node.IsDirty);
    }

    [TestMethod]
    public void DirtyFlag_CanBeMarkedAndClearedMultipleTimes()
    {
        var node = new TestNode();
        
        // Start dirty
        Assert.IsTrue(node.IsDirty);
        
        // Clear and verify
        node.ClearDirty();
        Assert.IsFalse(node.IsDirty);
        
        // Mark and verify
        node.MarkDirty();
        Assert.IsTrue(node.IsDirty);
        
        // Clear again
        node.ClearDirty();
        Assert.IsFalse(node.IsDirty);
        
        // Mark again
        node.MarkDirty();
        Assert.IsTrue(node.IsDirty);
    }

    #endregion

    #region NeedsRender Tests

    [TestMethod]
    public void NeedsRender_NewNode_ReturnsTrue()
    {
        var node = new TestNode();

        Assert.IsTrue(node.NeedsRender());
    }

    [TestMethod]
    public void NeedsRender_CleanNode_ReturnsFalse()
    {
        var node = new TestNode();
        node.ClearDirty();

        Assert.IsFalse(node.NeedsRender());
    }

    [TestMethod]
    public void NeedsRender_CleanNodeWithDirtyChild_ReturnsTrue()
    {
        var parent = new ContainerTestNode();
        var child = new TestNode(); // New, so dirty
        parent.Children.Add(child);
        parent.ClearDirty(); // Parent is clean

        Assert.IsTrue(parent.NeedsRender());
    }

    [TestMethod]
    public void NeedsRender_CleanNodeWithCleanChild_ReturnsFalse()
    {
        var parent = new ContainerTestNode();
        var child = new TestNode();
        parent.Children.Add(child);
        parent.ClearDirty();
        child.ClearDirty();

        Assert.IsFalse(parent.NeedsRender());
    }

    [TestMethod]
    public void NeedsRender_DirtyNodeWithCleanChild_ReturnsTrue()
    {
        var parent = new ContainerTestNode();
        var child = new TestNode();
        parent.Children.Add(child);
        child.ClearDirty();
        // Parent stays dirty

        Assert.IsTrue(parent.NeedsRender());
    }

    [TestMethod]
    public void NeedsRender_DeepNestedDirtyNode_ReturnsTrue()
    {
        var root = new ContainerTestNode();
        var level1 = new ContainerTestNode();
        var level2 = new ContainerTestNode();
        var leaf = new TestNode(); // Dirty
        
        root.Children.Add(level1);
        level1.Children.Add(level2);
        level2.Children.Add(leaf);
        
        root.ClearDirty();
        level1.ClearDirty();
        level2.ClearDirty();
        // leaf stays dirty

        Assert.IsTrue(root.NeedsRender());
    }

    #endregion
}

/// <summary>
/// Container node for testing parent-child dirty behavior.
/// </summary>
file sealed class ContainerTestNode : Hex1bNode
{
    public List<Hex1bNode> Children { get; } = new();
    
    protected override Size MeasureCore(Constraints constraints) => new(10, 10);
    public override void Render(Hex1bRenderContext context) { }
    public override IEnumerable<Hex1bNode> GetChildren() => Children;
}
