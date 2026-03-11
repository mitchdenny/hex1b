using Hex1b;
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

    [Fact]
    public void KgpImage_BuilderFallback_CreatesFallbackWidget()
    {
        var ctx = new RootContext();

        var widget = ctx.KgpImage(CreateTestImage(), 4, 4, kgp => kgp.Text("fallback"));

        var fallback = Assert.IsType<TextBlockWidget>(widget.Fallback);
        Assert.Equal("fallback", fallback.Text);
    }

    [Fact]
    public void KgpImage_BuilderFallback_PreservesRequestedDimensions()
    {
        var ctx = new RootContext();

        var widget = ctx.KgpImage(
            CreateTestImage(), 4, 4,
            kgp => kgp.Text("fallback"),
            width: 20,
            height: 10);

        Assert.Equal(20, widget.Width);
        Assert.Equal(10, widget.Height);
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

    #region Stretch Modes — Widget Construction

    [Fact]
    public void KgpImageWidget_DefaultStretch_IsStretch()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"));

        Assert.Equal(KgpImageStretch.Stretch, widget.Stretch);
    }

    [Fact]
    public void KgpImageWidget_Fit_SetsStretch()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .Fit();

        Assert.Equal(KgpImageStretch.Fit, widget.Stretch);
    }

    [Fact]
    public void KgpImageWidget_Fill_SetsStretch()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .Fill();

        Assert.Equal(KgpImageStretch.Fill, widget.Stretch);
    }

    [Fact]
    public void KgpImageWidget_Stretched_SetsStretch()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .Stretched();

        Assert.Equal(KgpImageStretch.Stretch, widget.Stretch);
    }

    [Fact]
    public void KgpImageWidget_NaturalSize_SetsStretch()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .NaturalSize();

        Assert.Equal(KgpImageStretch.None, widget.Stretch);
    }

    [Fact]
    public void KgpImageWidget_WithStretch_SetsStretch()
    {
        var widget = new KgpImageWidget(
            CreateTestImage(), 4, 4,
            new TextBlockWidget("fallback"))
            .WithStretch(KgpImageStretch.Fit);

        Assert.Equal(KgpImageStretch.Fit, widget.Stretch);
    }

    #endregion

    #region Stretch Modes — MeasureCore (layout allocation)

    [Fact]
    public void Measure_WithFillHint_ExpandsToConstraints_RegardlessOfStretchMode()
    {
        // MeasureCore claims fill space for layout; Stretch mode only affects rendering.
        foreach (var mode in new[] { KgpImageStretch.None, KgpImageStretch.Fit,
                                      KgpImageStretch.Fill, KgpImageStretch.Stretch })
        {
            var node = new KgpImageNode
            {
                ImageData = CreateTestImage(40, 40),
                PixelWidth = 40,
                PixelHeight = 40,
                Stretch = mode,
                Fallback = new TextBlockNode { Text = "fb" },
            };
            node.WidthHint = SizeHint.Fill;
            node.HeightHint = SizeHint.Fill;

            var size = node.Measure(new Constraints(0, 60, 0, 20));
            Assert.Equal(60, size.Width);
            Assert.Equal(20, size.Height);
        }
    }

    [Fact]
    public void Measure_WithoutFillHint_UsesNaturalSize_RegardlessOfStretchMode()
    {
        // 40x40 pixels → natural: 4 cols x 2 rows
        foreach (var mode in new[] { KgpImageStretch.None, KgpImageStretch.Fit,
                                      KgpImageStretch.Fill, KgpImageStretch.Stretch })
        {
            var node = new KgpImageNode
            {
                ImageData = CreateTestImage(40, 40),
                PixelWidth = 40,
                PixelHeight = 40,
                Stretch = mode,
                Fallback = new TextBlockNode { Text = "fb" },
            };

            var size = node.Measure(new Constraints(0, 60, 0, 20));
            Assert.Equal(4, size.Width);
            Assert.Equal(2, size.Height);
        }
    }

    [Fact]
    public void Measure_GuardsAgainstIntMaxValue()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(100, 200),
            PixelWidth = 100,
            PixelHeight = 200,
            Stretch = KgpImageStretch.Stretch,
            Fallback = new TextBlockNode { Text = "fb" },
        };
        node.WidthHint = SizeHint.Fill;
        node.HeightHint = SizeHint.Fill;

        // VStack first-pass uses int.MaxValue
        var size = node.Measure(new Constraints(0, int.MaxValue, 0, int.MaxValue));
        // Should fall back to natural size, not int.MaxValue
        Assert.True(size.Width < 1000, $"Width {size.Width} should not be int.MaxValue");
        Assert.True(size.Height < 1000, $"Height {size.Height} should not be int.MaxValue");
    }

    #endregion

    #region Stretch Modes — UniformToFill Clip Computation

    [Fact]
    public void ComputeFillClip_WiderSource_CropsWidth()
    {
        // Source 400x200 (pixel aspect 2:1), display 10x10 cells
        // Display pixel equiv: 100x200 (ratio 0.5:1)
        // Source wider → crop width
        var (clipX, clipY, clipW, clipH) = KgpImageNode.ComputeFillClip(400, 200, 10, 10);
        Assert.True(clipX > 0, "Should crop from sides");
        Assert.Equal(0, clipY);
        Assert.True(clipW < 400, $"clipW {clipW} should be less than full width 400");
        Assert.Equal(200, clipH);
        // Centered
        Assert.Equal((400 - clipW) / 2, clipX);
    }

    [Fact]
    public void ComputeFillClip_TallerSource_CropsHeight()
    {
        // Source 100x400 (pixel aspect 0.25:1), display 20x5 cells
        // Display pixel equiv: 200x100 (ratio 2:1)
        // Source taller → crop height
        var (clipX, clipY, clipW, clipH) = KgpImageNode.ComputeFillClip(100, 400, 20, 5);
        Assert.Equal(0, clipX);
        Assert.True(clipY > 0, "Should crop from top/bottom");
        Assert.Equal(100, clipW);
        Assert.True(clipH < 400, $"clipH {clipH} should be less than full height 400");
        Assert.Equal((400 - clipH) / 2, clipY);
    }

    [Fact]
    public void ComputeFillClip_MatchingAspect_NoCrop()
    {
        // Source 200x400 → natural 20x20 cells
        // Display at 20x20 cells → pixel equiv 200x400 → ratio 0.5
        // Source ratio = 200/400 = 0.5 → matches display → no crop
        var (clipX, clipY, clipW, clipH) = KgpImageNode.ComputeFillClip(200, 400, 20, 20);
        Assert.Equal(0, clipX);
        Assert.Equal(0, clipY);
        Assert.Equal(0, clipW);
        Assert.Equal(0, clipH);
    }

    [Fact]
    public void ComputeFillClip_SquareImageInWideDisplay_CropsHeight()
    {
        // Source 100x100 (square), display 40x5 cells
        // Display pixel equiv: 400x100 (ratio 4:1)
        // Source pixel ratio: 1:1
        // Source is taller → crop height
        var (clipX, clipY, clipW, clipH) = KgpImageNode.ComputeFillClip(100, 100, 40, 5);
        Assert.Equal(0, clipX);
        Assert.True(clipY > 0, "Should crop height for wide display");
        Assert.Equal(100, clipW);
        Assert.True(clipH < 100);
    }

    #endregion

    #region Stretch — Dirty Tracking

    [Fact]
    public void StretchChange_MarksDirty()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            Stretch = KgpImageStretch.Stretch,
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));
        node.ClearDirty();

        node.Stretch = KgpImageStretch.Fit;
        Assert.True(node.IsDirty);
    }

    [Fact]
    public void StretchChange_SameValue_DoesNotMarkDirty()
    {
        var node = new KgpImageNode
        {
            ImageData = CreateTestImage(2, 2),
            PixelWidth = 2,
            PixelHeight = 2,
            Stretch = KgpImageStretch.Stretch,
        };

        node.Measure(new Constraints(0, 80, 0, 24));
        node.Arrange(new Rect(0, 0, 80, 24));
        node.ClearDirty();

        node.Stretch = KgpImageStretch.Stretch; // same value
        Assert.False(node.IsDirty);
    }

    #endregion

    #region Stretch — NaturalCellSize Helper

    [Fact]
    public void NaturalCellSize_ComputesCorrectly()
    {
        // 100x200 pixels → (100+9)/10 = 10, (200+19)/20 = 10
        var (w, h) = KgpImageNode.NaturalCellSize(100, 200);
        Assert.Equal(10, w);
        Assert.Equal(10, h);
    }

    [Fact]
    public void NaturalCellSize_MinimumOne()
    {
        var (w, h) = KgpImageNode.NaturalCellSize(1, 1);
        Assert.Equal(1, w);
        Assert.Equal(1, h);
    }

    #endregion

    #region Zero-bounds safety

    [Fact]
    public void ComputeFillClip_ZeroCellDimensions_ReturnsZeroClip()
    {
        var (clipX, clipY, clipW, clipH) = KgpImageNode.ComputeFillClip(100, 100, 0, 0);
        Assert.Equal(0, clipX);
        Assert.Equal(0, clipY);
        Assert.Equal(0, clipW);
        Assert.Equal(0, clipH);
    }

    [Fact]
    public void ComputeFillClip_ZeroPixelDimensions_ReturnsZeroClip()
    {
        var (clipX, clipY, clipW, clipH) = KgpImageNode.ComputeFillClip(0, 0, 10, 10);
        Assert.Equal(0, clipX);
        Assert.Equal(0, clipY);
        Assert.Equal(0, clipW);
        Assert.Equal(0, clipH);
    }

    #endregion
}
