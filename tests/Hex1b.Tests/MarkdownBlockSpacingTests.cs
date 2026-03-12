using Hex1b.Markdown;

namespace Hex1b.Tests;

public class MarkdownBlockSpacingTests
{
    [Fact]
    public void NeedsSpacingBefore_FirstBlock_ReturnsFalse()
    {
        var block = new ParagraphBlock([], "Hello");
        Assert.False(MarkdownWidgetRenderer.NeedsSpacingBefore(null, block));
    }

    [Fact]
    public void NeedsSpacingBefore_HeadingThenParagraph_ReturnsFalse()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var paragraph = new ParagraphBlock([], "Text");
        Assert.False(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, paragraph));
    }

    [Fact]
    public void NeedsSpacingBefore_ParagraphThenParagraph_ReturnsTrue()
    {
        var p1 = new ParagraphBlock([], "First");
        var p2 = new ParagraphBlock([], "Second");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(p1, p2));
    }

    [Fact]
    public void NeedsSpacingBefore_ParagraphThenHeading_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var heading = new HeadingBlock(1, [], "Title");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, heading));
    }

    [Fact]
    public void NeedsSpacingBefore_HeadingThenList_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var list = new ListBlock(false, 1, []);
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, list));
    }

    [Fact]
    public void NeedsSpacingBefore_ParagraphThenCodeBlock_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var code = new FencedCodeBlock("csharp", "var x = 1;", "csharp");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, code));
    }

    [Fact]
    public void NeedsSpacingBefore_CodeBlockThenParagraph_ReturnsTrue()
    {
        var code = new FencedCodeBlock("csharp", "var x = 1;", "csharp");
        var paragraph = new ParagraphBlock([], "Text");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(code, paragraph));
    }

    [Fact]
    public void NeedsSpacingBefore_ParagraphThenBlockQuote_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var quote = new BlockQuoteBlock([new ParagraphBlock([], "Quoted")]);
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, quote));
    }

    [Fact]
    public void NeedsSpacingBefore_HeadingThenHeading_ReturnsTrue()
    {
        var h1 = new HeadingBlock(1, [], "First");
        var h2 = new HeadingBlock(2, [], "Second");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(h1, h2));
    }

    [Fact]
    public void NeedsSpacingBefore_ParagraphThenTable_ReturnsTrue()
    {
        var paragraph = new ParagraphBlock([], "Text");
        var table = new TableBlock([], [], []);
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(paragraph, table));
    }

    [Fact]
    public void NeedsSpacingBefore_HeadingThenFencedCode_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var code = new FencedCodeBlock("", "x = 1", "");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, code));
    }

    [Fact]
    public void NeedsSpacingBefore_HeadingThenIndentedCode_ReturnsTrue()
    {
        var heading = new HeadingBlock(1, [], "Title");
        var code = new IndentedCodeBlock("x = 1");
        Assert.True(MarkdownWidgetRenderer.NeedsSpacingBefore(heading, code));
    }
}
