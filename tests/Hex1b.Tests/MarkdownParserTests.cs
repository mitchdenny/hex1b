using Hex1b.Markdown;

namespace Hex1b.Tests;

public class MarkdownParserTests
{
    // --- Empty / minimal input ---

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyDocument()
    {
        var doc = MarkdownParser.Parse("");
        Assert.Empty(doc.Blocks);
    }

    [Fact]
    public void Parse_NullString_ReturnsEmptyDocument()
    {
        var doc = MarkdownParser.Parse((string)null!);
        Assert.Empty(doc.Blocks);
    }

    [Fact]
    public void Parse_BlankLines_ReturnsEmptyDocument()
    {
        var doc = MarkdownParser.Parse("\n\n\n");
        Assert.Empty(doc.Blocks);
    }

    // --- Headings ---

    [Theory]
    [InlineData("# H1", 1, "H1")]
    [InlineData("## H2", 2, "H2")]
    [InlineData("### H3", 3, "H3")]
    [InlineData("#### H4", 4, "H4")]
    [InlineData("##### H5", 5, "H5")]
    [InlineData("###### H6", 6, "H6")]
    public void Parse_Heading_ReturnsCorrectLevel(string input, int expectedLevel, string expectedText)
    {
        var doc = MarkdownParser.Parse(input);

        var heading = Assert.Single(doc.Blocks);
        var h = Assert.IsType<HeadingBlock>(heading);
        Assert.Equal(expectedLevel, h.Level);
        Assert.Equal(expectedText, h.Text);
    }

    [Fact]
    public void Parse_HeadingWithClosingHashes_StripsTrailingHashes()
    {
        var doc = MarkdownParser.Parse("## Hello ##");
        var h = Assert.IsType<HeadingBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("Hello", h.Text);
    }

    [Fact]
    public void Parse_HeadingNoSpace_NotAHeading()
    {
        var doc = MarkdownParser.Parse("#NotAHeading");
        var block = Assert.Single(doc.Blocks);
        Assert.IsType<ParagraphBlock>(block);
    }

    // --- Paragraphs ---

    [Fact]
    public void Parse_SimpleParagraph_ReturnsParagraphBlock()
    {
        var doc = MarkdownParser.Parse("Hello world");
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("Hello world", p.Text);
    }

    [Fact]
    public void Parse_MultiLineParagraph_JoinsLines()
    {
        var doc = MarkdownParser.Parse("Line one\nLine two");
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("Line one Line two", p.Text);
    }

    [Fact]
    public void Parse_TwoParagraphs_SeparatedByBlankLine()
    {
        var doc = MarkdownParser.Parse("First para\n\nSecond para");
        Assert.Equal(2, doc.Blocks.Count);
        Assert.IsType<ParagraphBlock>(doc.Blocks[0]);
        Assert.IsType<ParagraphBlock>(doc.Blocks[1]);
    }

    // --- Fenced Code Blocks ---

    [Fact]
    public void Parse_FencedCodeBlock_Backticks()
    {
        var input = "```\ncode here\n```";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("", code.Language);
        Assert.Equal("code here", code.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_WithLanguage()
    {
        var input = "```csharp\nvar x = 1;\n```";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("csharp", code.Language);
        Assert.Equal("var x = 1;", code.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_Tildes()
    {
        var input = "~~~\ncode\n~~~";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("code", code.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_MultipleLines()
    {
        var input = "```\nline 1\nline 2\nline 3\n```";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("line 1\nline 2\nline 3", code.Content);
    }

    [Fact]
    public void Parse_FencedCodeBlock_UnclosedTreatsRestAsCode()
    {
        var input = "```\ncode\nmore code";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<FencedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("code\nmore code", code.Content);
    }

    // --- Indented Code Blocks ---

    [Fact]
    public void Parse_IndentedCodeBlock_FourSpaces()
    {
        var input = "    code line";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<IndentedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("code line", code.Content);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_MultipleLines()
    {
        var input = "    line 1\n    line 2";
        var doc = MarkdownParser.Parse(input);
        var code = Assert.IsType<IndentedCodeBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("line 1\nline 2", code.Content);
    }

    // --- Block Quotes ---

    [Fact]
    public void Parse_BlockQuote_Simple()
    {
        var input = "> Hello";
        var doc = MarkdownParser.Parse(input);
        var bq = Assert.IsType<BlockQuoteBlock>(Assert.Single(doc.Blocks));
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(bq.Children));
        Assert.Equal("Hello", p.Text);
    }

    [Fact]
    public void Parse_BlockQuote_MultipleLines()
    {
        var input = "> Line 1\n> Line 2";
        var doc = MarkdownParser.Parse(input);
        var bq = Assert.IsType<BlockQuoteBlock>(Assert.Single(doc.Blocks));
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(bq.Children));
        Assert.Equal("Line 1 Line 2", p.Text);
    }

    [Fact]
    public void Parse_BlockQuote_Nested()
    {
        var input = "> > Nested";
        var doc = MarkdownParser.Parse(input);
        var outer = Assert.IsType<BlockQuoteBlock>(Assert.Single(doc.Blocks));
        var inner = Assert.IsType<BlockQuoteBlock>(Assert.Single(outer.Children));
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(inner.Children));
        Assert.Equal("Nested", p.Text);
    }

    // --- Unordered Lists ---

    [Fact]
    public void Parse_UnorderedList_Dashes()
    {
        var input = "- Item 1\n- Item 2\n- Item 3";
        var doc = MarkdownParser.Parse(input);
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.False(list.IsOrdered);
        Assert.Equal(3, list.Items.Count);
    }

    [Fact]
    public void Parse_UnorderedList_Asterisks()
    {
        var input = "* Item 1\n* Item 2";
        var doc = MarkdownParser.Parse(input);
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_UnorderedList_ItemContent()
    {
        var input = "- Hello world";
        var doc = MarkdownParser.Parse(input);
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        var item = Assert.Single(list.Items);
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(item.Children));
        Assert.Equal("Hello world", p.Text);
    }

    // --- Ordered Lists ---

    [Fact]
    public void Parse_OrderedList()
    {
        var input = "1. First\n2. Second\n3. Third";
        var doc = MarkdownParser.Parse(input);
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.True(list.IsOrdered);
        Assert.Equal(1, list.StartNumber);
        Assert.Equal(3, list.Items.Count);
    }

    [Fact]
    public void Parse_OrderedList_CustomStartNumber()
    {
        var input = "3. Third\n4. Fourth";
        var doc = MarkdownParser.Parse(input);
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.True(list.IsOrdered);
        Assert.Equal(3, list.StartNumber);
    }

    // --- Thematic Breaks ---

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    [InlineData("- - -")]
    [InlineData("* * *")]
    [InlineData("____")]
    public void Parse_ThematicBreak(string input)
    {
        var doc = MarkdownParser.Parse(input);
        Assert.IsType<ThematicBreakBlock>(Assert.Single(doc.Blocks));
    }

    [Fact]
    public void Parse_ThematicBreak_TwoCharsNotEnough()
    {
        var doc = MarkdownParser.Parse("--");
        Assert.IsNotType<ThematicBreakBlock>(Assert.Single(doc.Blocks));
    }

    // --- Inline Parsing ---

    [Fact]
    public void ParseInlines_PlainText()
    {
        var inlines = MarkdownParser.ParseInlines("Hello world");
        var text = Assert.IsType<TextInline>(Assert.Single(inlines));
        Assert.Equal("Hello world", text.Text);
    }

    [Fact]
    public void ParseInlines_Bold()
    {
        var inlines = MarkdownParser.ParseInlines("**bold**");
        var em = Assert.IsType<EmphasisInline>(Assert.Single(inlines));
        Assert.True(em.IsStrong);
        var text = Assert.IsType<TextInline>(Assert.Single(em.Children));
        Assert.Equal("bold", text.Text);
    }

    [Fact]
    public void ParseInlines_Italic()
    {
        var inlines = MarkdownParser.ParseInlines("*italic*");
        var em = Assert.IsType<EmphasisInline>(Assert.Single(inlines));
        Assert.False(em.IsStrong);
        var text = Assert.IsType<TextInline>(Assert.Single(em.Children));
        Assert.Equal("italic", text.Text);
    }

    [Fact]
    public void ParseInlines_CodeSpan()
    {
        var inlines = MarkdownParser.ParseInlines("`code`");
        var code = Assert.IsType<CodeInline>(Assert.Single(inlines));
        Assert.Equal("code", code.Code);
    }

    [Fact]
    public void ParseInlines_Link()
    {
        var inlines = MarkdownParser.ParseInlines("[click](https://example.com)");
        var link = Assert.IsType<LinkInline>(Assert.Single(inlines));
        Assert.Equal("click", link.Text);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void ParseInlines_LinkWithTitle()
    {
        var inlines = MarkdownParser.ParseInlines("[click](https://example.com \"Title\")");
        var link = Assert.IsType<LinkInline>(Assert.Single(inlines));
        Assert.Equal("click", link.Text);
        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("Title", link.Title);
    }

    [Fact]
    public void ParseInlines_Image()
    {
        var inlines = MarkdownParser.ParseInlines("![alt text](image.png)");
        var image = Assert.IsType<ImageInline>(Assert.Single(inlines));
        Assert.Equal("alt text", image.AltText);
        Assert.Equal("image.png", image.Url);
    }

    [Fact]
    public void ParseInlines_MixedContent()
    {
        var inlines = MarkdownParser.ParseInlines("Hello **bold** and `code` here");
        Assert.Equal(5, inlines.Count);
        Assert.IsType<TextInline>(inlines[0]);
        Assert.IsType<EmphasisInline>(inlines[1]);
        Assert.IsType<TextInline>(inlines[2]);
        Assert.IsType<CodeInline>(inlines[3]);
        Assert.IsType<TextInline>(inlines[4]);
    }

    // --- Complex documents ---

    [Fact]
    public void Parse_MixedDocument_AllBlockTypes()
    {
        var input = """
            # Title

            A paragraph.

            ```python
            print("hello")
            ```

            > A quote

            - Item A
            - Item B

            1. One
            2. Two

            ---

            Final paragraph.
            """;

        var doc = MarkdownParser.Parse(input);

        // Verify we got all expected block types
        Assert.IsType<HeadingBlock>(doc.Blocks[0]);
        Assert.IsType<ParagraphBlock>(doc.Blocks[1]);
        Assert.IsType<FencedCodeBlock>(doc.Blocks[2]);
        Assert.IsType<BlockQuoteBlock>(doc.Blocks[3]);
        Assert.IsType<ListBlock>(doc.Blocks[4]);
        Assert.IsType<ListBlock>(doc.Blocks[5]);
        Assert.IsType<ThematicBreakBlock>(doc.Blocks[6]);
        Assert.IsType<ParagraphBlock>(doc.Blocks[7]);
    }

    [Fact]
    public void Parse_BlockQuoteWithCode_NestedCorrectly()
    {
        var input = "> ```\n> code\n> ```";
        var doc = MarkdownParser.Parse(input);
        var bq = Assert.IsType<BlockQuoteBlock>(Assert.Single(doc.Blocks));
        Assert.IsType<FencedCodeBlock>(Assert.Single(bq.Children));
    }

    // --- ReadOnlyMemory overload ---

    [Fact]
    public void Parse_ReadOnlyMemory_ProducesEquivalentResult()
    {
        var source = "# Hello\n\nWorld";
        var memory = source.AsMemory();

        var docFromString = MarkdownParser.Parse(source);
        var docFromMemory = MarkdownParser.Parse(memory);

        Assert.Equal(docFromString.Blocks.Count, docFromMemory.Blocks.Count);
    }

    // --- Edge cases ---

    [Fact]
    public void Parse_NoTrailingNewline_HandledCorrectly()
    {
        var doc = MarkdownParser.Parse("# Hello");
        var h = Assert.IsType<HeadingBlock>(Assert.Single(doc.Blocks));
        Assert.Equal("Hello", h.Text);
    }

    [Fact]
    public void Parse_OnlyWhitespace_ReturnsEmpty()
    {
        var doc = MarkdownParser.Parse("   \n   \n   ");
        Assert.Empty(doc.Blocks);
    }
}
