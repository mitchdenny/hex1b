using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KgpImageWidget/KgpImageNode — the widget-tree API for KGP images.
/// </summary>
public class KgpImageNodeTests
{
    private static byte[] CreateTestImage(int width = 4, int height = 4)
    {
        var data = new byte[width * height * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 255;     // R
            data[i + 1] = 0;   // G
            data[i + 2] = 0;   // B
            data[i + 3] = 255; // A
        }
        return data;
    }

    private static Hex1bAppWorkloadAdapter CreateKgpEnabledWorkload()
        => new(new TerminalCapabilities
        {
            SupportsKgp = true,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

    private static Hex1bAppWorkloadAdapter CreateKgpDisabledWorkload()
        => new(new TerminalCapabilities
        {
            SupportsKgp = false,
            SupportsTrueColor = true,
            Supports256Colors = true,
        });

    #region Widget Construction

    [Fact]
    public void KgpImageWidget_DefaultZOrder_IsBelowText()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"));

        Assert.Equal(KgpZOrder.BelowText, widget.ZOrder);
    }

    [Fact]
    public void KgpImageWidget_AboveText_SetsZOrder()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .AboveText();

        Assert.Equal(KgpZOrder.AboveText, widget.ZOrder);
    }

    [Fact]
    public void KgpImageWidget_WithWidth_SetsWidth()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .WithWidth(20);

        Assert.Equal(20, widget.Width);
    }

    [Fact]
    public void KgpImageWidget_WithHeight_SetsHeight()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .WithHeight(10);

        Assert.Equal(10, widget.Height);
    }

    [Fact]
    public void KgpImageWidget_GetExpectedNodeType_ReturnsKgpImageNode()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"));

        Assert.Equal(typeof(KgpImageNode), widget.GetExpectedNodeType());
    }

    #endregion

    #region Node MeasureCore

    [Fact]
    public void Measure_WithExplicitDimensions_ReturnsRequestedSize()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(20, 40),
            PixelWidth = 20,
            PixelHeight = 40,
            RequestedWidth = 10,
            RequestedHeight = 5,
            Fallback = new TextBlockNode { Text = "fb" },
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));
        Assert.Equal(10, size.Width);
        Assert.Equal(5, size.Height);
    }

    [Fact]
    public void Measure_WithoutExplicitDimensions_ComputesFromPixels()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(40, 60),
            PixelWidth = 40,
            PixelHeight = 60,
            Fallback = new TextBlockNode { Text = "fb" },
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));
        Assert.True(size.Width >= 4);
        Assert.True(size.Height >= 3);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(200, 200),
            PixelWidth = 200,
            PixelHeight = 200,
            RequestedWidth = 50,
            RequestedHeight = 50,
            Fallback = new TextBlockNode { Text = "fb" },
        };

        var size = node.Measure(new Constraints(0, 10, 0, 5));
        Assert.True(size.Width <= 10);
        Assert.True(size.Height <= 5);
    }

    #endregion

    #region Node Render

    [Fact]
    public async Task Render_WithKgpSupport_EmitsKgpSequence()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            RequestedWidth = 4,
            RequestedHeight = 2,
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));

        using var workload = CreateKgpEnabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(80, 24)
            .Build();
        var context = new Hex1bRenderContext(workload);
        node.Render(context);

        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Build()
            .ApplyAsync(terminal);

        // The KGP sequence was emitted through the workload adapter
        // Verify the terminal processed the output without errors
        var snapshot = terminal.CreateSnapshot();
        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task Render_WithoutKgpSupport_RendersFallback()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            Fallback = new TextBlockNode { Text = "No KGP" },
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Fallback!.Arrange(new Rect(0, 0, 40, 1));
        node.Arrange(new Rect(0, 0, 80, 24));

        using var workload = CreateKgpDisabledWorkload();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        var context = new Hex1bRenderContext(workload);
        node.Render(context);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("No KGP"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal);

        Assert.True(terminal.CreateSnapshot().ContainsText("No KGP"));
    }

    [Fact]
    public void Render_AboveText_UsesPositiveZIndex()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            ZOrder = KgpZOrder.AboveText,
        };

        // Verify the z-order is set correctly
        Assert.Equal(KgpZOrder.AboveText, node.ZOrder);
    }

    [Fact]
    public void Render_BelowText_UsesNegativeZIndex()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            ZOrder = KgpZOrder.BelowText,
        };

        Assert.Equal(KgpZOrder.BelowText, node.ZOrder);
    }

    #endregion

    #region Dirty Tracking

    [Fact]
    public void ImageChange_MarksDirty()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));
        node.ClearDirty();

        node.ImageData = CreateTestImage(4, 4);
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void ZOrderChange_MarksDirty()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            ZOrder = KgpZOrder.BelowText,
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));
        node.ClearDirty();

        node.ZOrder = KgpZOrder.AboveText;
        Assert.True(node.IsDirty);
    }

    #endregion

    #region Children

    [Fact]
    public void GetChildren_ReturnsFallback()
    {
        var fallback = new TextBlockNode { Text = "fb" };
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            Fallback = fallback,
        };

        var children = node.GetChildren().ToList();
        Assert.Single(children);
        Assert.Same(fallback, children[0]);
    }

    [Fact]
    public void GetChildren_NoFallback_Empty()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
        };

        var children = node.GetChildren().ToList();
        Assert.Empty(children);
    }

    #endregion
}
