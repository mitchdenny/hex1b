#pragma warning disable HEX1B_COMPOSITION // Experimental API

using Hex1b;
using Hex1b.Composition;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests.Composition;

public class Hex1bCompositeNodeTests
{
    // --- Layout pass-through (mirrors StatePanelNode tests) ---

    [Fact]
    public void Measure_WithChild_DelegatesToChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new Hex1bCompositeNode { Child = child };

        var size = node.Measure(new Constraints(0, 50, 0, 5));

        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    [Fact]
    public void Measure_WithoutChild_ReturnsConstrainedZero()
    {
        var node = new Hex1bCompositeNode();

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Arrange_PassesThroughToChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new Hex1bCompositeNode { Child = child };
        node.Measure(Constraints.Unbounded);

        var rect = new Rect(5, 10, 30, 1);
        node.Arrange(rect);

        Assert.Equal(rect, node.Bounds);
        Assert.Equal(rect, child.Bounds);
    }

    [Fact]
    public void GetChildren_ReturnsChildOrEmpty()
    {
        var empty = new Hex1bCompositeNode();
        Assert.Empty(empty.GetChildren());

        var child = new TextBlockNode { Text = "x" };
        var withChild = new Hex1bCompositeNode { Child = child };
        Assert.Equal(new[] { (Hex1bNode)child }, withChild.GetChildren());
    }

    [Fact]
    public void GetFocusableNodes_DelegatesToChild()
    {
        var button = new ButtonNode { Label = "OK" };
        var node = new Hex1bCompositeNode { Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Same(button, focusables[0]);
    }

    [Fact]
    public void Render_WithoutChild_DoesNothing()
    {
        var surface = new Surface(20, 3);
        var ctx = new SurfaceRenderContext(surface);
        var node = new Hex1bCompositeNode();
        node.Arrange(new Rect(0, 0, 20, 3));

        var ex = Record.Exception(() => ctx.RenderChild(node));

        Assert.Null(ex);
    }
}
