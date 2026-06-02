using Hex1b;
using Hex1b.Composition;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;
using Hex1b.Widgets;

namespace Hex1b.Tests.Composition;

[TestClass]
public class Hex1bCompositeNodeTests
{
    // --- Layout pass-through (mirrors StatePanelNode tests) ---

    [TestMethod]
    public void Measure_WithChild_DelegatesToChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new Hex1bCompositeNode { Child = child };

        var size = node.Measure(new Constraints(0, 50, 0, 5));

        Assert.IsTrue(size.Width > 0);
        Assert.IsTrue(size.Height > 0);
    }

    [TestMethod]
    public void Measure_WithoutChild_ReturnsConstrainedZero()
    {
        var node = new Hex1bCompositeNode();

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    [TestMethod]
    public void Arrange_PassesThroughToChild()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new Hex1bCompositeNode { Child = child };
        node.Measure(Constraints.Unbounded);

        var rect = new Rect(5, 10, 30, 1);
        node.Arrange(rect);

        Assert.AreEqual(rect, node.Bounds);
        Assert.AreEqual(rect, child.Bounds);
    }

    [TestMethod]
    public void GetChildren_ReturnsChildOrEmpty()
    {
        var empty = new Hex1bCompositeNode();
        Assert.IsEmpty(empty.GetChildren());

        var child = new TextBlockNode { Text = "x" };
        var withChild = new Hex1bCompositeNode { Child = child };
        TestSeq.AreEqual(new[] { (Hex1bNode)child }, withChild.GetChildren());
    }

    [TestMethod]
    public void GetFocusableNodes_DelegatesToChild()
    {
        var button = new ButtonNode { Label = "OK" };
        var node = new Hex1bCompositeNode { Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        TestSeq.Single(focusables);
        Assert.AreSame(button, focusables[0]);
    }

    [TestMethod]
    public void Render_WithoutChild_DoesNothing()
    {
        var surface = new Surface(20, 3);
        var ctx = new SurfaceRenderContext(surface);
        var node = new Hex1bCompositeNode();
        node.Arrange(new Rect(0, 0, 20, 3));

        var ex = TestSeq.RecordException(() => ctx.RenderChild(node));

        Assert.IsNull(ex);
    }
}
