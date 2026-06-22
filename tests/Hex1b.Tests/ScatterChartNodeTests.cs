using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;

namespace Hex1b.Tests;

[TestClass]
public class ScatterChartNodeTests
{
    [TestMethod]
    public void Render_TitleWithWideCharacters_UsesDisplayWidth()
    {
        var node = new ScatterChartNode<Point>
        {
            Data = [new Point(0, 0), new Point(1, 1)],
            XSelector = p => p.X,
            YSelector = p => p.Y,
            Title = "播放",
        };
        node.Measure(new Constraints(0, 12, 0, 8));
        node.Arrange(new Rect(0, 0, 12, 8));

        var surface = new Surface(12, 8);
        node.Render(new SurfaceRenderContext(surface));

        Assert.AreEqual("播", surface[4, 0].Character);
        Assert.IsTrue(surface[5, 0].IsContinuation);
        Assert.AreEqual("放", surface[6, 0].Character);
        Assert.IsTrue(surface[7, 0].IsContinuation);
    }

    private sealed record Point(double X, double Y);
}
