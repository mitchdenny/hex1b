using Hex1b.Documents;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class OverlayEnhancementTests
{
    [Fact]
    public void OverlaySegment_Construction_SetsProperties()
    {
        var seg = new OverlaySegment("hello", Hex1bColor.Red, Hex1bColor.Blue)
        {
            IsBold = true,
            IsItalic = true
        };

        Assert.Equal("hello", seg.Text);
        Assert.Equal(Hex1bColor.Red, seg.Foreground);
        Assert.Equal(Hex1bColor.Blue, seg.Background);
        Assert.True(seg.IsBold);
        Assert.True(seg.IsItalic);
    }

    [Fact]
    public void OverlaySegment_Defaults_NoStyling()
    {
        var seg = new OverlaySegment("plain");
        Assert.Null(seg.Foreground);
        Assert.Null(seg.Background);
        Assert.False(seg.IsBold);
        Assert.False(seg.IsItalic);
    }

    [Fact]
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

        Assert.NotNull(line.Segments);
        Assert.Equal(2, line.Segments.Count);
    }

    [Fact]
    public void OverlayLine_WithoutSegments_UsesTextProperty()
    {
        var line = new OverlayLine("simple text", Hex1bColor.White);
        Assert.Null(line.Segments);
        Assert.Equal("simple text", line.Text);
    }

    [Fact]
    public void EditorOverlay_MaxWidth_SetsConstraint()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, [])
        { MaxWidth = 40 };

        Assert.Equal(40, overlay.MaxWidth);
    }

    [Fact]
    public void EditorOverlay_MaxHeight_SetsConstraint()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, [])
        { MaxHeight = 10 };

        Assert.Equal(10, overlay.MaxHeight);
    }

    [Fact]
    public void EditorOverlay_Title_SetsProperty()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, [])
        { Title = "Hover Info" };

        Assert.Equal("Hover Info", overlay.Title);
    }

    [Fact]
    public void EditorOverlay_Defaults_NullConstraints()
    {
        var overlay = new EditorOverlay(
            "test", new DocumentPosition(1, 1), OverlayPlacement.Below, []);

        Assert.Null(overlay.MaxWidth);
        Assert.Null(overlay.MaxHeight);
        Assert.Null(overlay.Title);
    }

    [Fact]
    public void OverlayTheme_HasExpectedDefaults()
    {
        var bg = OverlayTheme.BackgroundColor.DefaultValue();
        var fg = OverlayTheme.ForegroundColor.DefaultValue();
        var border = OverlayTheme.BorderColor.DefaultValue();

        Assert.NotEqual(Hex1bColor.Default, bg);
        Assert.NotEqual(Hex1bColor.Default, fg);
        Assert.NotEqual(Hex1bColor.Default, border);
    }

    [Fact]
    public void OverlaySegment_RecordEquality()
    {
        var a = new OverlaySegment("text", Hex1bColor.Red);
        var b = new OverlaySegment("text", Hex1bColor.Red);
        Assert.Equal(a, b);
    }

    [Fact]
    public void OverlaySegment_RecordInequality()
    {
        var a = new OverlaySegment("text", Hex1bColor.Red);
        var b = new OverlaySegment("text", Hex1bColor.Blue);
        Assert.NotEqual(a, b);
    }
}
