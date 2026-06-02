using Hex1b.Documents;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class OverlayEnhancementTests
{
    [TestMethod]
    public void OverlaySegment_Construction_SetsProperties()
    {
        var seg = new OverlaySegment("hello", Hex1bColor.Red, Hex1bColor.Blue)
        {
            IsBold = true,
            IsItalic = true
        };

        Assert.AreEqual("hello", seg.Text);
        Assert.AreEqual(Hex1bColor.Red, seg.Foreground);
        Assert.AreEqual(Hex1bColor.Blue, seg.Background);
        Assert.IsTrue(seg.IsBold);
        Assert.IsTrue(seg.IsItalic);
    }

    [TestMethod]
    public void OverlaySegment_Defaults_NoStyling()
    {
        var seg = new OverlaySegment("plain");
        Assert.IsNull(seg.Foreground);
        Assert.IsNull(seg.Background);
        Assert.IsFalse(seg.IsBold);
        Assert.IsFalse(seg.IsItalic);
    }

    [TestMethod]
    public void OverlayLine_WithSegments_HasRichContent()
    {
        var line = new OverlayLine("full text")
        {
            Segments =
            [
                new OverlaySegment("bold ") { IsBold = true },
                new OverlaySegment("and italic") { IsItalic = true }
            ]
        };

        Assert.IsNotNull(line.Segments);
        Assert.AreEqual(2, line.Segments.Count);
    }

    [TestMethod]
    public void OverlayLine_WithoutSegments_UsesTextProperty()
    {
        var line = new OverlayLine("simple text", Hex1bColor.White);
        Assert.IsNull(line.Segments);
        Assert.AreEqual("simple text", line.Text);
    }

    [TestMethod]
    public void EditorOverlay_MaxWidth_SetsConstraint()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, [])
        { MaxWidth = 40 };

        Assert.AreEqual(40, overlay.MaxWidth);
    }

    [TestMethod]
    public void EditorOverlay_MaxHeight_SetsConstraint()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, [])
        { MaxHeight = 10 };

        Assert.AreEqual(10, overlay.MaxHeight);
    }

    [TestMethod]
    public void EditorOverlay_Title_SetsProperty()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, [])
        { Title = "Hover Info" };

        Assert.AreEqual("Hover Info", overlay.Title);
    }

    [TestMethod]
    public void EditorOverlay_Defaults_NullConstraints()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, []);

        Assert.IsNull(overlay.MaxWidth);
        Assert.IsNull(overlay.MaxHeight);
        Assert.IsNull(overlay.Title);
    }

    [TestMethod]
    public void OverlayTheme_HasExpectedDefaults()
    {
        var bg = OverlayTheme.BackgroundColor.DefaultValue();
        var fg = OverlayTheme.ForegroundColor.DefaultValue();
        var border = OverlayTheme.BorderColor.DefaultValue();

        Assert.AreNotEqual(Hex1bColor.Default, bg);
        Assert.AreNotEqual(Hex1bColor.Default, fg);
        Assert.AreNotEqual(Hex1bColor.Default, border);
    }

    [TestMethod]
    public void OverlaySegment_RecordEquality()
    {
        var a = new OverlaySegment("text", Hex1bColor.Red);
        var b = new OverlaySegment("text", Hex1bColor.Red);
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void OverlaySegment_RecordInequality()
    {
        var a = new OverlaySegment("text", Hex1bColor.Red);
        var b = new OverlaySegment("text", Hex1bColor.Blue);
        Assert.AreNotEqual(a, b);
    }
}
