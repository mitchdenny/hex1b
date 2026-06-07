using Hex1b.Charts;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Surfaces;

namespace Hex1b.Tests;

[TestClass]
public class LegendNodeTests
{
    private static LegendNode<ChartItem> CreateNode(
        IReadOnlyList<ChartItem> data,
        bool showValues = false,
        bool showPercentages = false,
        bool horizontal = false,
        Func<double, string>? valueFormatter = null)
    {
        return new LegendNode<ChartItem>
        {
            Data = data,
            LabelSelector = i => i.Label,
            ValueSelector = i => i.Value,
            ShowValues = showValues,
            ShowPercentages = showPercentages,
            IsHorizontal = horizontal,
            ValueFormatter = valueFormatter,
        };
    }

    private static ChartItem[] SampleData =>
    [
        new("Go", 42),
        new("Rust", 28),
        new("C#", 30),
    ];

    #region Measure Tests — Vertical

    [TestMethod]
    public void Measure_Vertical_HeightEqualsItemCount()
    {
        var node = CreateNode(SampleData);
        var size = node.Measure(new Constraints(0, 40, 0, 30));

        Assert.AreEqual(3, size.Height);
    }

    [TestMethod]
    public void Measure_Vertical_WidthFitsLabels()
    {
        var node = CreateNode(SampleData);
        var size = node.Measure(Constraints.Unbounded);

        // "■ " (2) + longest label "Rust" (4) = 6 minimum
        Assert.IsTrue(size.Width >= 6);
    }

    [TestMethod]
    public void Measure_Vertical_WideCharacterLabels_UsesDisplayWidth()
    {
        var node = CreateNode([new("播放", 10)]);
        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(6, size.Width); // "■ " (2) + CJK label width (4)
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_Vertical_RespectsMaxHeight()
    {
        var node = CreateNode(SampleData);
        var size = node.Measure(new Constraints(0, 40, 0, 2));

        Assert.IsTrue(size.Height <= 2);
    }

    [TestMethod]
    public void Measure_EmptyData_ReturnsZeroSize()
    {
        var node = CreateNode([]);
        var size = node.Measure(new Constraints(0, 40, 0, 30));

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(0, size.Height);
    }

    #endregion

    #region Measure Tests — Horizontal

    [TestMethod]
    public void Measure_Horizontal_HeightIsOne()
    {
        var node = CreateNode(SampleData, horizontal: true);
        var size = node.Measure(new Constraints(0, 100, 0, 30));

        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public void Measure_Horizontal_WidthAccommodatesAllItems()
    {
        var node = CreateNode(SampleData, horizontal: true);
        var size = node.Measure(Constraints.Unbounded);

        // Each item: swatch(1) + space(1) + label. Plus spacing(2) between items.
        // "■ Go" (4) + "  " (2) + "■ Rust" (6) + "  " (2) + "■ C#" (4) = 18
        Assert.IsTrue(size.Width >= 18);
    }

    #endregion

    #region Render Tests — Vertical

    [TestMethod]
    public void Render_Vertical_ShowsLabels()
    {
        var node = CreateNode(SampleData);
        var (surface, size) = RenderNode(node, 40, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("Go", allText);
        Assert.Contains("Rust", allText);
        Assert.Contains("C#", allText);
    }

    [TestMethod]
    public void Render_Vertical_ShowsSwatchCharacter()
    {
        var node = CreateNode(SampleData);
        var (surface, _) = RenderNode(node, 40, 10);

        // First column should have swatch character ■
        Assert.AreEqual("■", surface[0, 0].Character);
        Assert.AreEqual("■", surface[0, 1].Character);
        Assert.AreEqual("■", surface[0, 2].Character);
    }

    [TestMethod]
    public void Render_Vertical_WideCharacterLabel_WritesContinuationCells()
    {
        var node = CreateNode([new("播放", 10)]);
        var (surface, _) = RenderNode(node, 20, 2);

        Assert.AreEqual("播", surface[2, 0].Character);
        Assert.AreEqual(2, surface[2, 0].DisplayWidth);
        Assert.IsTrue(surface[3, 0].IsContinuation);
        Assert.AreEqual("放", surface[4, 0].Character);
        Assert.AreEqual(2, surface[4, 0].DisplayWidth);
        Assert.IsTrue(surface[5, 0].IsContinuation);
    }

    [TestMethod]
    public void Render_Vertical_SwatchHasForegroundColor()
    {
        var node = CreateNode(SampleData);
        var (surface, _) = RenderNode(node, 40, 10);

        // Swatch should use foreground color (not background)
        Assert.IsNotNull(surface[0, 0].Foreground);
        Assert.IsNotNull(surface[0, 1].Foreground);
        Assert.IsNotNull(surface[0, 2].Foreground);
    }

    [TestMethod]
    public void Render_Vertical_DifferentSwatchColors()
    {
        var node = CreateNode(SampleData);
        var (surface, _) = RenderNode(node, 40, 10);

        var color0 = surface[0, 0].Foreground;
        var color1 = surface[0, 1].Foreground;
        var color2 = surface[0, 2].Foreground;

        // All three segments should have different colors
        Assert.AreNotEqual(color0, color1);
        Assert.AreNotEqual(color1, color2);
    }

    [TestMethod]
    public void Render_ShowValues_DisplaysValues()
    {
        var node = CreateNode(SampleData, showValues: true);
        var (surface, size) = RenderNode(node, 40, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("42", allText);
        Assert.Contains("28", allText);
    }

    [TestMethod]
    public void Render_ShowPercentages_DisplaysPercent()
    {
        var node = CreateNode(SampleData, showPercentages: true);
        var (surface, size) = RenderNode(node, 40, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("%", allText);
    }

    [TestMethod]
    public void Render_ShowBoth_DisplaysValuesAndPercentages()
    {
        var node = CreateNode(SampleData, showValues: true, showPercentages: true);
        var (surface, size) = RenderNode(node, 60, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("42", allText);
        Assert.Contains("%", allText);
    }

    [TestMethod]
    public void Render_CustomFormatter_UsedInOutput()
    {
        var node = CreateNode(SampleData, showValues: true, valueFormatter: v => $"${v:F0}");
        var (surface, size) = RenderNode(node, 50, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("$42", allText);
    }

    #endregion

    #region Render Tests — Horizontal

    [TestMethod]
    public void Render_Horizontal_AllItemsOnOneRow()
    {
        var node = CreateNode(SampleData, horizontal: true);
        var (surface, _) = RenderNode(node, 60, 1);

        var row = GetRowText(surface, 0);
        Assert.Contains("Go", row);
        Assert.Contains("Rust", row);
        Assert.Contains("C#", row);
    }

    [TestMethod]
    public void Render_Horizontal_ShowsSwatches()
    {
        var node = CreateNode(SampleData, horizontal: true);
        var (surface, _) = RenderNode(node, 60, 1);

        // First character should be swatch
        Assert.AreEqual("■", surface[0, 0].Character);
    }

    [TestMethod]
    public void Render_Horizontal_WithPercentages()
    {
        var node = CreateNode(SampleData, showPercentages: true, horizontal: true);
        var (surface, _) = RenderNode(node, 80, 1);

        var row = GetRowText(surface, 0);
        Assert.Contains("%", row);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void Render_NullData_DoesNotCrash()
    {
        var node = new LegendNode<ChartItem>
        {
            Data = null,
            LabelSelector = i => i.Label,
            ValueSelector = i => i.Value,
        };
        node.Measure(new Constraints(0, 40, 0, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        var surface = new Surface(40, 10);
        var context = new SurfaceRenderContext(surface);
        node.Render(context);
        // Should not throw
    }

    [TestMethod]
    public void Render_ZeroValues_Excluded()
    {
        var data = new ChartItem[]
        {
            new("Visible", 50),
            new("Zero", 0),
            new("Also Visible", 50),
        };
        var node = CreateNode(data);
        var (surface, size) = RenderNode(node, 40, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("Visible", allText);
        Assert.DoesNotContain("Zero", allText);
    }

    [TestMethod]
    public void Render_SingleItem_Works()
    {
        var node = CreateNode([new("Only", 100)]);
        var (surface, size) = RenderNode(node, 40, 10);

        var allText = GetAllText(surface, size.Height);
        Assert.Contains("Only", allText);
    }

    [TestMethod]
    public void Render_VeryNarrowWidth_DoesNotCrash()
    {
        var node = CreateNode(SampleData);
        node.Measure(new Constraints(0, 3, 0, 10));
        node.Arrange(new Rect(0, 0, 3, 10));

        var surface = new Surface(3, 10);
        var context = new SurfaceRenderContext(surface);
        node.Render(context);
        // Should not throw, but 3 cols = just swatch + space + 1 char
    }

    #endregion

    #region Helpers

    private static (Surface surface, Size size) RenderNode(LegendNode<ChartItem> node, int width, int height)
    {
        var size = node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, size.Width, size.Height));

        var surface = new Surface(width, height);
        var context = new SurfaceRenderContext(surface);
        node.Render(context);

        return (surface, size);
    }

    private static string GetRowText(Surface surface, int row)
    {
        var chars = new List<char>();
        for (int x = 0; x < surface.Width; x++)
        {
            var cell = surface[x, row];
            var ch = cell.Character;
            if (ch is not null && ch.Length > 0)
                chars.Add(ch[0]);
            else
                chars.Add(' ');
        }
        return new string(chars.ToArray());
    }

    private static string GetAllText(Surface surface, int maxHeight)
    {
        var lines = new List<string>();
        for (int y = 0; y < Math.Min(surface.Height, maxHeight); y++)
            lines.Add(GetRowText(surface, y));
        return string.Join("\n", lines);
    }

    #endregion
}
