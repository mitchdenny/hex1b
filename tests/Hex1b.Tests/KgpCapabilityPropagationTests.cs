using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for KGP capability propagation through the surface render context and layer system.
/// </summary>
[TestClass]
public class KgpCapabilityPropagationTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsSixel = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
    };

    [TestMethod]
    public void SurfaceRenderContext_DefaultCapabilities_AreModern()
    {
        var surface = new Surface(10, 5);
        var context = new SurfaceRenderContext(surface);

        // Default should be Modern (no KGP)
        Assert.IsFalse(context.Capabilities.SupportsKgp);
        Assert.IsFalse(context.Capabilities.SupportsSixel);
    }

    [TestMethod]
    public void SurfaceRenderContext_SetCapabilities_AreReturned()
    {
        var surface = new Surface(10, 5);
        var context = new SurfaceRenderContext(surface);
        context.SetCapabilities(KgpCapabilities);

        Assert.IsTrue(context.Capabilities.SupportsKgp);
        Assert.IsTrue(context.Capabilities.SupportsSixel);
    }

    [TestMethod]
    public void SurfaceRenderContext_RenderChild_PropagatesCapabilities()
    {
        var surface = new Surface(20, 10);
        var context = new SurfaceRenderContext(surface);
        context.SetCapabilities(KgpCapabilities);

        TerminalCapabilities? childCapabilities = null;

        // Create a node that captures the capabilities it sees during render
        var childNode = new CapabilityCapturingNode(caps => childCapabilities = caps);
        childNode.Measure(new Constraints(0, 20, 0, 10));
        childNode.Arrange(new Rect(0, 0, 10, 5));

        context.RenderChild(childNode);

        Assert.IsNotNull(childCapabilities);
        Assert.IsTrue(childCapabilities!.SupportsKgp);
        Assert.IsTrue(childCapabilities.SupportsSixel);
    }

    [TestMethod]
    public void SurfaceNode_PassesCapabilitiesToLayerContext()
    {
        TerminalCapabilities? layerCapabilities = null;

        var node = new SurfaceNode
        {
            LayerBuilder = ctx =>
            {
                layerCapabilities = ctx.Capabilities;
                return [];
            }
        };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 10, 5));

        var surface = new Surface(20, 10);
        var context = new SurfaceRenderContext(surface);
        context.SetCapabilities(KgpCapabilities);

        node.Render(context);

        Assert.IsNotNull(layerCapabilities);
        Assert.IsTrue(layerCapabilities!.SupportsKgp);
    }

    [TestMethod]
    public void SurfaceNode_WithoutKgp_LayerBuilderSeesNoKgp()
    {
        TerminalCapabilities? layerCapabilities = null;

        var node = new SurfaceNode
        {
            LayerBuilder = ctx =>
            {
                layerCapabilities = ctx.Capabilities;
                return [];
            }
        };
        node.Measure(new Constraints(0, 20, 0, 10));
        node.Arrange(new Rect(0, 0, 10, 5));

        var surface = new Surface(20, 10);
        var context = new SurfaceRenderContext(surface);
        // Don't set KGP capabilities

        node.Render(context);

        Assert.IsNotNull(layerCapabilities);
        Assert.IsFalse(layerCapabilities!.SupportsKgp);
    }

    /// <summary>
    /// A test node that captures capabilities from the render context during render.
    /// </summary>
    private sealed class CapabilityCapturingNode : Hex1bNode
    {
        private readonly Action<TerminalCapabilities> _onCapabilities;

        public CapabilityCapturingNode(Action<TerminalCapabilities> onCapabilities)
        {
            _onCapabilities = onCapabilities;
        }

        protected override Size MeasureCore(Constraints constraints)
            => constraints.Constrain(new Size(10, 5));

        public override void Render(Hex1bRenderContext context)
        {
            _onCapabilities(context.Capabilities);
        }
    }
}
