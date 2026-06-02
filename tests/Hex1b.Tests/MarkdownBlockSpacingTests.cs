using Hex1b.Markdown;

namespace Hex1b.Tests;

[TestClass]
public class MarkdownBlockSpacingTests
{
    [TestMethod]
    public void NeedsSpacingBefore_FirstBlock_ReturnsFalse()
    {
        var block = new ParagraphBlock([], "Hello");
        Assert.IsFalse(MarkdownWidgetRenderer.NeedsSpacingBefore(null, block));
    }

    [TestMethod]
    public void NeedsSpacingBefore_HeadingThenParagraph_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var paragraph = new ParagraphBlock([], "Text");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, paragraph));
    }

    [TestMethod]
    public void NeedsSpacingBefore_ParagraphThenParagraph_ReturnsTrue()
    {
        var p1 = new ParagraphBlock([], "First");
        var p2 = new ParagraphBlock([], "Second");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(p1, p2));
    }

    [TestMethod]
    public void NeedsSpacingBefore_ParagraphThenHeading_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var heading = new HeadingBlock(1, [], "Title");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, heading));
    }

    [TestMethod]
    public void NeedsSpacingBefore_HeadingThenList_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var list = new ListBlock(false, 1, []);
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, list));
    }

    [TestMethod]
    public void NeedsSpacingBefore_ParagraphThenCodeBlock_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var code = new FencedCodeBlock("csharp", "var x = 1;", "csharp");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, code));
    }

    [TestMethod]
    public void NeedsSpacingBefore_CodeBlockThenParagraph_ReturnsTrue()
    {
        var code = new FencedCodeBlock("csharp", "var x = 1;", "csharp");
        var paragraph = new ParagraphBlock([], "Text");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(code, paragraph));
    }

    [TestMethod]
    public void NeedsSpacingBefore_ParagraphThenBlockQuote_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var quote = new BlockQuoteBlock([new ParagraphBlock([], "Quoted")]);
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, quote));
    }

    [TestMethod]
    public void NeedsSpacingBefore_HeadingThenHeading_ReturnsTrue()
    {
        var h1 = new HeadingBlock(1, [], "First");
        var h2 = new HeadingBlock(2, [], "Second");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(h1, h2));
    }

    [TestMethod]
    public void NeedsSpacingBefore_ParagraphThenTable_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var table = new TableBlock([], [], []);
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, table));
    }

    [TestMethod]
    public void NeedsSpacingBefore_HeadingThenFencedCode_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var code = new FencedCodeBlock("", "x = 1", "");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, code));
    }

    [TestMethod]
    public void NeedsSpacingBefore_HeadingThenIndentedCode_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var code = new IndentedCodeBlock("x = 1");
        Assert.IsTrue(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, code));
    }
}
