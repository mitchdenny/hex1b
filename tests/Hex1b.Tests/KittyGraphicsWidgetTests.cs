using Hex1b.Kgp;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class KittyGraphicsWidgetTests
{
    // =============================================
    // Widget creation tests
    // =============================================

    [Fact]
    public void Widget_CreatesWithCorrectProperties()
    {
        var data = new byte[16]; // 2x2 RGBA
        var widget = new KittyGraphicsWidget(data, 2, 2, KgpFormat.Rgba32);

        Assert.Same(data, widget.PixelData);
        Assert.Equal(2u, widget.PixelWidth);
        Assert.Equal(2u, widget.PixelHeight);
        Assert.Equal(KgpFormat.Rgba32, widget.Format);
        Assert.Equal(0u, widget.DisplayColumns);
        Assert.Equal(0u, widget.DisplayRows);
    }

    [Fact]
    public void Widget_DefaultFormat_IsRgba32()
    {
        var data = new byte[16];
        var widget = new KittyGraphicsWidget(data, 2, 2);

        Assert.Equal(KgpFormat.Rgba32, widget.Format);
    }

    [Fact]
    public void Widget_WithDisplaySize_SetsColumnsAndRows()
    {
        var data = new byte[16];
        var widget = new KittyGraphicsWidget(data, 2, 2)
            .WithDisplaySize(10, 5);

        Assert.Equal(10u, widget.DisplayColumns);
        Assert.Equal(5u, widget.DisplayRows);
    }

    [Fact]
    public void Widget_GetExpectedNodeType_ReturnsKittyGraphicsNode()
    {
        var data = new byte[16];
        var widget = new KittyGraphicsWidget(data, 2, 2);

        Assert.Equal(typeof(KittyGraphicsNode), widget.GetExpectedNodeType());
    }

    // =============================================
    // Node measurement tests
    // =============================================

    [Fact]
    public void Node_Measure_ExplicitSize_ReturnsCorrectDimensions()
    {
        var node = new KittyGraphicsNode
        {
            PixelData = new byte[16],
            PixelWidth = 2,
            PixelHeight = 2,
            DisplayColumns = 8,
            DisplayRows = 4,
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        Assert.Equal(8, size.Width);
        Assert.Equal(4, size.Height);
    }

    [Fact]
    public void Node_Measure_AutoSize_EstimatesFromPixels()
    {
        var node = new KittyGraphicsNode
        {
            PixelData = new byte[64 * 32 * 4],
            PixelWidth = 64,
            PixelHeight = 32,
            DisplayColumns = 0, // auto
            DisplayRows = 0,    // auto
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        // 64 pixels / 8 pixels per cell = 8 columns
        Assert.Equal(8, size.Width);
        // 32 pixels / 16 pixels per cell = 2 rows
        Assert.Equal(2, size.Height);
    }

    [Fact]
    public void Node_Measure_ConstrainedByMax()
    {
        var node = new KittyGraphicsNode
        {
            PixelData = new byte[16],
            PixelWidth = 2,
            PixelHeight = 2,
            DisplayColumns = 100,
            DisplayRows = 50,
        };

        var size = node.Measure(new Constraints(0, 40, 0, 20));

        Assert.Equal(40, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Node_Measure_MinimumOneCell()
    {
        var node = new KittyGraphicsNode
        {
            PixelData = new byte[4],
            PixelWidth = 1,
            PixelHeight = 1,
            DisplayColumns = 0,
            DisplayRows = 0,
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        Assert.True(size.Width >= 1);
        Assert.True(size.Height >= 1);
    }

    // =============================================
    // Reconciliation tests
    // =============================================

    [Fact]
    public async Task Reconcile_CreatesNewNode()
    {
        var data = new byte[16];
        var widget = new KittyGraphicsWidget(data, 2, 2);

        var node = await widget.ReconcileAsync(null, ReconcileContext.CreateRoot());

        Assert.IsType<KittyGraphicsNode>(node);
        var kgpNode = (KittyGraphicsNode)node;
        Assert.Same(data, kgpNode.PixelData);
        Assert.Equal(2u, kgpNode.PixelWidth);
        Assert.Equal(2u, kgpNode.PixelHeight);
    }

    [Fact]
    public async Task Reconcile_ReusesExistingNode()
    {
        var data1 = new byte[16];
        var widget1 = new KittyGraphicsWidget(data1, 2, 2);
        var node1 = await widget1.ReconcileAsync(null, ReconcileContext.CreateRoot());

        var data2 = new byte[36];
        var widget2 = new KittyGraphicsWidget(data2, 3, 3);
        var node2 = await widget2.ReconcileAsync(node1, ReconcileContext.CreateRoot());

        Assert.Same(node1, node2);
        var kgpNode = (KittyGraphicsNode)node2;
        Assert.Same(data2, kgpNode.PixelData);
        Assert.Equal(3u, kgpNode.PixelWidth);
    }

    [Fact]
    public async Task Reconcile_SameData_ReusesNode()
    {
        var data = new byte[16];
        var widget = new KittyGraphicsWidget(data, 2, 2, KgpFormat.Rgba32, 4, 2);
        var node = (KittyGraphicsNode)await widget.ReconcileAsync(null, ReconcileContext.CreateRoot());

        // Reconcile same widget again — should reuse same node
        var widget2 = new KittyGraphicsWidget(data, 2, 2, KgpFormat.Rgba32, 4, 2);
        var node2 = await widget2.ReconcileAsync(node, ReconcileContext.CreateRoot());

        Assert.Same(node, node2);
    }

    [Fact]
    public async Task Reconcile_DifferentData_MarksDirty()
    {
        var data1 = new byte[16];
        var widget1 = new KittyGraphicsWidget(data1, 2, 2);
        var node = (KittyGraphicsNode)await widget1.ReconcileAsync(null, ReconcileContext.CreateRoot());

        var data2 = new byte[36];
        var widget2 = new KittyGraphicsWidget(data2, 3, 3);
        await widget2.ReconcileAsync(node, ReconcileContext.CreateRoot());

        Assert.True(node.IsDirty);
    }

    // =============================================
    // Render tests
    // =============================================

    [Fact]
    public void Render_NodeBoundsSetCorrectly()
    {
        var node = new KittyGraphicsNode
        {
            PixelData = new byte[16], // 2x2 RGBA
            PixelWidth = 2,
            PixelHeight = 2,
            DisplayColumns = 4,
            DisplayRows = 2,
        };

        node.Measure(new Constraints(0, 40, 0, 20));
        node.Arrange(new Rect(5, 3, 4, 2));

        Assert.Equal(5, node.Bounds.X);
        Assert.Equal(3, node.Bounds.Y);
        Assert.Equal(4, node.Bounds.Width);
        Assert.Equal(2, node.Bounds.Height);
    }

    [Fact]
    public void Render_EmptyData_DoesNotThrow()
    {
        var node = new KittyGraphicsNode
        {
            PixelData = [],
            PixelWidth = 0,
            PixelHeight = 0,
        };

        node.Measure(new Constraints(0, 40, 0, 20));
        node.Arrange(new Rect(0, 0, 4, 2));

        // Should not throw when rendering with empty data
    }

    // =============================================
    // Extension method tests
    // =============================================

    [Fact]
    public void WithDisplaySize_ReturnsNewWidgetWithSize()
    {
        var data = new byte[16];
        var widget = new KittyGraphicsWidget(data, 2, 2);

        var sized = widget.WithDisplaySize(10, 5);

        Assert.Equal(10u, sized.DisplayColumns);
        Assert.Equal(5u, sized.DisplayRows);
        // Original unchanged
        Assert.Equal(0u, widget.DisplayColumns);
    }
}
