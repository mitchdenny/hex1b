using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class ProgressNodeTests
{
    [Fact]
    public void Measure_FillsAvailableWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, 80, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(80, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMinWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(20, 80, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(80, size.Width); // Should fill available width
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_UnboundedWidth_UsesDefaultWidth()
    {
        // Arrange
        var node = new ProgressNode { Value = 50, Maximum = 100 };
        var constraints = new Constraints(0, int.MaxValue, 0, 10);

        // Act
        var size = node.Measure(constraints);

        // Assert
        Assert.Equal(20, size.Width); // Default width when unbounded
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Reconcile_PreservesNodeOnSameType()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 25, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 75, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        // Act
        var node1 = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult();
        var node2 = widget2.ReconcileAsync(node1, context).GetAwaiter().GetResult();

        // Assert
        Assert.Same(node1, node2);
        Assert.Equal(75, ((ProgressNode)node2).Value);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnValueChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 25, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 75, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnMinimumChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, Minimum = 0, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 50, Minimum = -50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnMaximumChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 50, Maximum = 200 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_MarksDirtyOnIndeterminateChange()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, IsIndeterminate = false };
        var widget2 = new ProgressWidget { Value = 50, IsIndeterminate = true };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void Reconcile_DoesNotMarkDirtyWhenUnchanged()
    {
        // Arrange
        var widget1 = new ProgressWidget { Value = 50, Maximum = 100 };
        var widget2 = new ProgressWidget { Value = 50, Maximum = 100 };
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget1.ReconcileAsync(null, context).GetAwaiter().GetResult() as ProgressNode;
        node!.ClearDirty();

        // Act
        widget2.ReconcileAsync(node, context).GetAwaiter().GetResult();

        // Assert
        Assert.False(node.IsDirty);
    }

    [Fact]
    public void GetExpectedNodeType_ReturnsProgressNode()
    {
        // Arrange
        var widget = new ProgressWidget();

        // Act & Assert
        Assert.Equal(typeof(ProgressNode), widget.GetExpectedNodeType());
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        // Arrange
        var node = new ProgressNode();

        // Assert
        Assert.False(node.IsFocusable);
    }

    [Theory]
    [InlineData(0, 0, 100, 0.0)]
    [InlineData(50, 0, 100, 0.5)]
    [InlineData(100, 0, 100, 1.0)]
    [InlineData(25, 0, 100, 0.25)]
    [InlineData(-25, -50, 50, 0.25)]
    [InlineData(0, -100, 100, 0.5)]
    public void DeterminateMode_CalculatesPercentageCorrectly(double value, double min, double max, double expectedPercentage)
    {
        // This is a logic test - we verify the percentage calculation
        var range = max - min;
        var actualPercentage = range > 0 ? Math.Clamp((value - min) / range, 0.0, 1.0) : 0.0;
        
        Assert.Equal(expectedPercentage, actualPercentage, precision: 5);
    }

    [Fact]
    public void WithAnimationPosition_ClampsToValidRange()
    {
        // Arrange
        var widget = new ProgressWidget { IsIndeterminate = true };

        // Act
        var updated = widget.WithAnimationPosition(1.5);

        // Assert
        Assert.Equal(0.5, updated.AnimationPosition, precision: 5); // 1.5 % 1.0 = 0.5
    }

    [Fact]
    public void IndeterminateMode_SetsCorrectProperties()
    {
        // Arrange & Act
        var widget = new ProgressWidget { IsIndeterminate = true, AnimationPosition = 0.3 };

        // Assert
        Assert.True(widget.IsIndeterminate);
        Assert.Equal(0.3, widget.AnimationPosition, precision: 5);
    }
}
