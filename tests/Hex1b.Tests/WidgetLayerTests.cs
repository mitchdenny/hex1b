using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class WidgetLayerTests
{
    [TestMethod]
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

        Assert.AreEqual("H", surface[0, 0].Character);
        Assert.AreEqual("e", surface[1, 0].Character);
        Assert.AreEqual("l", surface[2, 0].Character);
        Assert.AreEqual("l", surface[3, 0].Character);
        Assert.AreEqual("o", surface[4, 0].Character);
    }

    [TestMethod]
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

        Assert.AreEqual("T", surface2[0, 0].Character);
        Assert.AreEqual("e", surface2[1, 0].Character);
    }

    [TestMethod]
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
            Assert.AreEqual("X", surface[i, 0].Character);
        }
    }

    [TestMethod]
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
        Assert.AreEqual("B", surface[0, 0].Character);
        Assert.AreEqual("B", surface[1, 0].Character);
    }

    [TestMethod]
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
        Assert.AreEqual("F", surface1[0, 0].Character);

        // Change the widget tree
        text = "Second";
        node.LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget(text))];

        var surface2 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface2));
        Assert.AreEqual("S", surface2[0, 0].Character);
    }

    [TestMethod]
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

        Assert.AreEqual("H", surface[5, 2].Character);
        Assert.AreEqual("i", surface[6, 2].Character);
    }

    [TestMethod]
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
        Assert.AreEqual("Z", surface[0, 0].Character);
        Assert.AreEqual("Z", surface[1, 0].Character);
        // Remaining should be from widget layer
        Assert.AreEqual("l", surface[2, 0].Character);
    }

    [TestMethod]
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

    [TestMethod]
    public void Render_WidgetLayerRemovedBetweenFrames_CleansUpStaleState()
    {
        // Frame 1: two widget layers
        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            [
                s.WidgetLayer(new TextBlockWidget("One")),
                s.WidgetLayer(new TextBlockWidget("Two"))
            ]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface1 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface1));

        // Frame 2: only one widget layer — should not throw and stale state is cleaned
        node.LayerBuilder = s => [s.WidgetLayer(new TextBlockWidget("Only"))];

        var surface2 = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface2));

        Assert.AreEqual("O", surface2[0, 0].Character);
        Assert.AreEqual("n", surface2[1, 0].Character);
    }

    [TestMethod]
    public void Render_WidgetLayerWithVStack_RendersMultiLineLayout()
    {
        var node = new SurfaceNode
        {
            LayerBuilder = s => [s.WidgetLayer(new VStackWidget([
                new TextBlockWidget("Line1"),
                new TextBlockWidget("Line2"),
            ]))]
        };
        node.Arrange(new Rect(0, 0, 10, 2));

        var surface = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface));

        Assert.AreEqual("L", surface[0, 0].Character);
        Assert.AreEqual("L", surface[0, 1].Character);
        Assert.AreEqual("2", surface[4, 1].Character);
    }

    [TestMethod]
    public void Render_WidgetLayerComputedLayerReadsWidgetContent()
    {
        // Computed layer reads widget layer content and transforms foreground
        var tintColor = Hex1bColor.Red;
        var node = new SurfaceNode
        {
            LayerBuilder = s =>
            [
                s.WidgetLayer(new TextBlockWidget("Test")),
                s.Layer(ctx =>
                {
                    var below = ctx.GetBelow();
                    return below with { Foreground = tintColor };
                })
            ]
        };
        node.Arrange(new Rect(0, 0, 10, 1));

        var surface = new Surface(80, 24);
        node.Render(new SurfaceRenderContext(surface));

        // Characters from widget layer, foreground replaced by computed layer
        Assert.AreEqual("T", surface[0, 0].Character);
        Assert.AreEqual(tintColor, surface[0, 0].Foreground);
        Assert.AreEqual("e", surface[1, 0].Character);
        Assert.AreEqual(tintColor, surface[1, 0].Foreground);
    }
}
