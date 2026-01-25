using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for QrCodeNode rendering and measurement.
/// </summary>
public class QrCodeNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSizeForUrl()
    {
        var node = new QrCodeNode { Data = "https://github.com/mitchdenny/hex1b" };

        var size = node.Measure(Constraints.Unbounded);

        // QR code for this URL should be non-zero
        Assert.True(size.Width > 0, "Width should be greater than 0");
        Assert.True(size.Height > 0, "Height should be greater than 0");
        Assert.Equal(size.Width, size.Height); // QR codes are square
    }

    [Fact]
    public void Measure_EmptyData_ReturnsZeroSize()
    {
        var node = new QrCodeNode { Data = "" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_WithQuietZone_IncludesQuietZoneInSize()
    {
        var nodeWithoutQuietZone = new QrCodeNode { Data = "test", QuietZone = 0 };
        var nodeWithQuietZone = new QrCodeNode { Data = "test", QuietZone = 2 };

        var sizeWithout = nodeWithoutQuietZone.Measure(Constraints.Unbounded);
        var sizeWith = nodeWithQuietZone.Measure(Constraints.Unbounded);

        // With quiet zone should be larger by 2 * quietZone (both sides)
        Assert.Equal(sizeWithout.Width + 4, sizeWith.Width);
        Assert.Equal(sizeWithout.Height + 4, sizeWith.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new QrCodeNode { Data = "test" };

        var size = node.Measure(new Constraints(0, 10, 0, 10));

        Assert.True(size.Width <= 10, $"Width {size.Width} should be <= 10");
        Assert.True(size.Height <= 10, $"Height {size.Height} should be <= 10");
    }

    #endregion

    #region Property Tests

    [Fact]
    public void Data_WhenChanged_MarksNodeDirty()
    {
        var node = new QrCodeNode { Data = "initial" };
        node.ClearDirty();

        node.Data = "changed";

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void QuietZone_WhenChanged_MarksNodeDirty()
    {
        var node = new QrCodeNode { QuietZone = 1 };
        node.ClearDirty();

        node.QuietZone = 2;

        Assert.True(node.IsDirty);
    }

    [Fact]
    public void QuietZone_NegativeValue_ClampsToZero()
    {
        var node = new QrCodeNode { QuietZone = -5 };

        Assert.Equal(0, node.QuietZone);
    }

    #endregion

    #region Widget Reconciliation Tests

    [Fact]
    public void QrCodeWidget_Reconcile_CreatesNewNode()
    {
        var widget = new QrCodeWidget("https://example.com");
        var context = ReconcileContext.CreateRoot(new FocusRing());

        var node = widget.ReconcileAsync(null, context).GetAwaiter().GetResult();

        Assert.IsType<QrCodeNode>(node);
        var qrNode = (QrCodeNode)node;
        Assert.Equal("https://example.com", qrNode.Data);
        Assert.Equal(1, qrNode.QuietZone); // Default quiet zone
    }

    [Fact]
    public void QrCodeWidget_Reconcile_UpdatesExistingNode()
    {
        var widget = new QrCodeWidget("https://example.com", QuietZone: 0);
        var context = ReconcileContext.CreateRoot(new FocusRing());
        var existingNode = new QrCodeNode { Data = "old", QuietZone = 1 };

        var node = widget.ReconcileAsync(existingNode, context).GetAwaiter().GetResult();

        Assert.Same(existingNode, node);
        Assert.Equal("https://example.com", existingNode.Data);
        Assert.Equal(0, existingNode.QuietZone);
    }

    [Fact]
    public void QrCodeWidget_Reconcile_MarksNodeDirtyWhenDataChanges()
    {
        var widget = new QrCodeWidget("new-data");
        var context = ReconcileContext.CreateRoot(new FocusRing());
        var existingNode = new QrCodeNode { Data = "old-data" };
        existingNode.ClearDirty();

        widget.ReconcileAsync(existingNode, context).GetAwaiter().GetResult();

        Assert.True(existingNode.IsDirty);
    }

    #endregion
}
