using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class SurfaceNodeTests
{
    [Fact]
    public void Measure_WithFillHints_ReturnsMaxConstraints()
    {
        var node = new SurfaceNode
        {
            LayerBuilder = _ => [],
            WidthHint = SizeHint.Fill,
            HeightHint = SizeHint.Fill
        };

        var size = node.Measure(Constraints.Loose(80, 24));

        Assert.Equal(80, size.Width);
        Assert.Equal(24, size.Height);
    }

    [Fact]
    public void Measure_WithFixedHints_ReturnsFixedSize()
    {
        var node = new SurfaceNode
        {
            LayerBuilder = _ => [],
            WidthHint = SizeHint.Fixed(40),
            HeightHint = SizeHint.Fixed(10)
        };

        var size = node.Measure(Constraints.Loose(80, 24));

        Assert.Equal(40, size.Width);
        Assert.Equal(10, size.Height);
    }

    [Fact]
    public void Measure_WithFixedHints_ClampsToConstraints()
    {
        var node = new SurfaceNode
        {
            LayerBuilder = _ => [],
            WidthHint = SizeHint.Fixed(100), // Larger than constraint
            HeightHint = SizeHint.Fixed(50)
        };

        var size = node.Measure(Constraints.Loose(80, 24));

        Assert.Equal(80, size.Width); // Clamped to max
        Assert.Equal(24, size.Height); // Clamped to max
    }

    [Fact]
    public void Render_WithNoLayers_DoesNotThrow()
    {
        var node = new SurfaceNode { LayerBuilder = _ => [] };
        node.Arrange(new Rect(0, 0, 10, 5));

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        // Should not throw
        node.Render(context);
    }

    [Fact]
    public void Render_WithNullLayerBuilder_DoesNotThrow()
    {
        var node = new SurfaceNode { LayerBuilder = null };
        node.Arrange(new Rect(0, 0, 10, 5));

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        // Should not throw
        node.Render(context);
    }

    [Fact]
    public void Render_WithZeroSizeBounds_DoesNotThrow()
    {
        var node = new SurfaceNode { LayerBuilder = _ => [new SourceSurfaceLayer(new Surface(1, 1), 0, 0)] };
        node.Arrange(new Rect(0, 0, 0, 0)); // Zero size

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        // Should not throw
        node.Render(context);
    }

    [Fact]
    public void Render_SingleStaticLayer_WritesToContext()
    {
        // Create a surface with some content
        var sourceSurface = new Surface(5, 1);
        sourceSurface.WriteText(0, 0, "Hello");

        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.Layer(sourceSurface)]
        };
        node.Arrange(new Rect(0, 0, 5, 1));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        // Check that content was written
        Assert.Equal("H", targetSurface[0, 0].Character);
        Assert.Equal("e", targetSurface[1, 0].Character);
        Assert.Equal("l", targetSurface[2, 0].Character);
        Assert.Equal("l", targetSurface[3, 0].Character);
        Assert.Equal("o", targetSurface[4, 0].Character);
    }

    [Fact]
    public void Render_LayerWithOffset_PositionsCorrectly()
    {
        var sourceSurface = new Surface(3, 1);
        sourceSurface.WriteText(0, 0, "ABC");

        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.Layer(sourceSurface, offsetX: 5, offsetY: 2)]
        };
        node.Arrange(new Rect(0, 0, 10, 5));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        // Content should be at offset position
        Assert.Equal("A", targetSurface[5, 2].Character);
        Assert.Equal("B", targetSurface[6, 2].Character);
        Assert.Equal("C", targetSurface[7, 2].Character);
    }

    [Fact]
    public void Render_DrawLayer_ExecutesCallback()
    {
        var drawWasCalled = false;

        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.Layer(surface =>
            {
                drawWasCalled = true;
                surface.WriteText(0, 0, "Drawn");
            })]
        };
        node.Arrange(new Rect(0, 0, 10, 5));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        Assert.True(drawWasCalled);
        Assert.Equal("D", targetSurface[0, 0].Character);
    }

    [Fact]
    public void Render_ComputedLayer_ExecutesForEachCell()
    {
        // Create a computed layer that makes all cells 'X'
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.Layer(_ => new SurfaceCell("X", null, null))]
        };
        node.Arrange(new Rect(0, 0, 3, 2));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        // All cells should be 'X'
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                Assert.Equal("X", targetSurface[x, y].Character);
            }
        }
    }

    [Fact]
    public void Render_MultipleLayers_CompositesInOrder()
    {
        // Background layer with 'A' everywhere
        var background = new Surface(5, 1);
        background.Fill(new Rect(0, 0, 5, 1), new SurfaceCell("A", null, null));

        // Foreground layer with 'B' at position 2
        var foreground = new Surface(1, 1);
        foreground.WriteText(0, 0, "B");

        var node = new SurfaceNode
        {
            LayerBuilder = s => [
                s.Layer(background),
                s.Layer(foreground, offsetX: 2, offsetY: 0)
            ]
        };
        node.Arrange(new Rect(0, 0, 5, 1));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        Assert.Equal("A", targetSurface[0, 0].Character);
        Assert.Equal("A", targetSurface[1, 0].Character);
        Assert.Equal("B", targetSurface[2, 0].Character); // Foreground overwrites
        Assert.Equal("A", targetSurface[3, 0].Character);
        Assert.Equal("A", targetSurface[4, 0].Character);
    }

    [Fact]
    public void Render_ComputedLayerCanQueryBelow()
    {
        // Base layer with 'A'
        var baseSurface = new Surface(3, 1);
        baseSurface.WriteText(0, 0, "ABC");

        // Computed layer that replaces with uppercase version of what's below
        // (just to verify GetBelow works)
        var node = new SurfaceNode
        {
            LayerBuilder = s => [
                s.Layer(baseSurface),
                s.Layer(ctx => {
                    // Get the cell below and replace with 'X'
                    var below = ctx.GetBelow();
                    // If there's something below, return 'X', otherwise return space
                    if (below.Character != SurfaceCells.UnwrittenMarker && below.Character != " ")
                        return new SurfaceCell("X", null, null);
                    return new SurfaceCell(" ", null, null);
                })
            ]
        };
        node.Arrange(new Rect(0, 0, 3, 1));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        // All cells should be replaced with 'X' since the computed layer could see the base
        Assert.Equal("X", targetSurface[0, 0].Character);
        Assert.Equal("X", targetSurface[1, 0].Character);
        Assert.Equal("X", targetSurface[2, 0].Character);
    }

    [Fact]
    public void SurfaceLayerContext_ProvidesCorrectDimensions()
    {
        int capturedWidth = 0, capturedHeight = 0;

        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            {
                capturedWidth = s.Width;
                capturedHeight = s.Height;
                return [];
            }
        };
        node.Arrange(new Rect(0, 0, 40, 12));

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        node.Render(context);

        Assert.Equal(40, capturedWidth);
        Assert.Equal(12, capturedHeight);
    }

    [Fact]
    public void SurfaceLayerContext_ProvidesTheme()
    {
        Hex1bTheme? capturedTheme = null;

        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            {
                capturedTheme = s.Theme;
                return [];
            }
        };
        node.Arrange(new Rect(0, 0, 10, 5));

        var surface = new Surface(80, 24);
        var theme = Hex1bThemes.Default;
        var context = new SurfaceRenderContext(surface, theme);

        node.Render(context);

        Assert.NotNull(capturedTheme);
    }

    [Fact]
    public void Render_CellWithColor_WritesWithAnsiCodes()
    {
        var sourceSurface = new Surface(1, 1);
        sourceSurface[0, 0] = new SurfaceCell("X", Hex1bColor.Red, Hex1bColor.Blue);

        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.Layer(sourceSurface)]
        };
        node.Arrange(new Rect(0, 0, 1, 1));

        var targetSurface = new Surface(80, 24);
        var context = new SurfaceRenderContext(targetSurface);

        node.Render(context);

        // The cell should have colors applied
        var cell = targetSurface[0, 0];
        Assert.Equal("X", cell.Character);
        Assert.Equal(Hex1bColor.Red, cell.Foreground);
        Assert.Equal(Hex1bColor.Blue, cell.Background);
    }

    [Fact]
    public void GetChildren_ReturnsEmpty()
    {
        var node = new SurfaceNode();

        var children = node.GetChildren().ToList();

        Assert.Empty(children);
    }
}
