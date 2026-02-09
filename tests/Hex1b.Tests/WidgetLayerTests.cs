using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class WidgetLayerTests
{
    [Fact]
    public void Render_WidgetLayer_RendersTextBlockToSurface()
    {
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget("Hello"))]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        node.Render(context);

        Assert.Equal("H", surface[0, 0].Character);
        Assert.Equal("e", surface[1, 0].Character);
        Assert.Equal("l", surface[2, 0].Character);
        Assert.Equal("l", surface[3, 0].Character);
        Assert.Equal("o", surface[4, 0].Character);
    }

    [Fact]
    public void Render_WidgetLayer_PreservesNodeStateAcrossFrames()
    {
        // Render twice with the same widget — should not throw and should reuse node state
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget("Test"))]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface1 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface1));

        var surface2 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface2));

        Assert.Equal("T", surface2[0, 0].Character);
        Assert.Equal("e", surface2[1, 0].Character);
    }

    [Fact]
    public void Render_WidgetLayerWithComputedLayer_CompositesCorrectly()
    {
        // Widget layer on bottom, computed layer on top that replaces all content with 'X'
        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            [
                s.WidgetLayer(new TextBlockWidget("Hello")),
                s.Layer(ctx =>
                {
                    var below = ctx.GetBelow();
                    if (below.Character != SurfaceCells.UnwrittenMarker && below.Character != " ")
                        return new SurfaceCell("X", null, null);
                    return below;
                })
            ]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface));

        // "Hello" should be replaced with "XXXXX"
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal("X", surface[i, 0].Character);
        }
    }

    [Fact]
    public void Render_MultipleWidgetLayers_CompositesInOrder()
    {
        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            [
                s.WidgetLayer(new TextBlockWidget("AAAA")),
                s.WidgetLayer(new TextBlockWidget("BB"))
            ]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface));

        // Top layer "BB" should overwrite first 2 chars of "AAAA"
        Assert.Equal("B", surface[0, 0].Character);
        Assert.Equal("B", surface[1, 0].Character);
    }

    [Fact]
    public void Render_WidgetLayerChangingWidget_UpdatesContent()
    {
        string text = "First";
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget(text))]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface1 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface1));
        Assert.Equal("F", surface1[0, 0].Character);

        // Change the widget tree
        text = "Second";
        node.LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget(text))];

        var surface2 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface2));
        Assert.Equal("S", surface2[0, 0].Character);
    }

    [Fact]
    public void Render_WidgetLayerAtOffset_WritesAtCorrectPosition()
    {
        // SurfaceNode positioned at (5, 2) in the target
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget("Hi"))]
        };
        node.Arrange(new Rect(5, 2, 10, 1));

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        node.Render(context);

        Assert.Equal("H", surface[5, 2].Character);
        Assert.Equal("i", surface[6, 2].Character);
    }

    [Fact]
    public void Render_WidgetLayerBelowStaticLayer_CompositesCorrectly()
    {
        // Widget layer as background, static source layer on top
        var overlay = new Surface(2, 1);
        overlay.WriteText(0, 0, "ZZ");

        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            [
                s.WidgetLayer(new TextBlockWidget("Hello")),
                s.Layer(overlay)
            ]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface));

        // "ZZ" overlay should replace first 2 chars
        Assert.Equal("Z", surface[0, 0].Character);
        Assert.Equal("Z", surface[1, 0].Character);
        // Remaining should be from widget layer
        Assert.Equal("l", surface[2, 0].Character);
    }

    [Fact]
    public void Render_EmptyWidgetTree_DoesNotThrow()
    {
        // VStack with no children — should render without crashing
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.WidgetLayer(new VStackWidget([]))]
        };
        node.Arrange(new Rect(0, 0, 10, 5));

        var surface = new Surface(80, 24);
        var context = new SurfaceRenderContext(surface);

        // Should not throw
        node.Render(context);
    }
}
