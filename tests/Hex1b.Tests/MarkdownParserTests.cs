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

    // --- Diagnostic: nested list parsing ---

    [Fact]
    public void Parse_NestedList_ProducesNestedStructure()
    {
        var md = "- Item 1\n  - Nested A\n  - Nested B\n- Item 2";
        var doc = MarkdownParser.Parse(md);

        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(2, list.Items.Count);

        // Item 1 should have a paragraph + nested list
        var item1 = list.Items[0];
        Assert.True(item1.Children.Count >= 2,
            $"Expected paragraph + nested list, got {item1.Children.Count} children: " +
            string.Join(", ", item1.Children.Select(c => c.GetType().Name)));

        var nestedList = item1.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.NotNull(nestedList);
        Assert.Equal(2, nestedList.Items.Count);
    }

    [Fact]
    public void Parse_NestedList_ThreeLevels()
    {
        var md = "- Level 0\n  - Level 1\n    - Level 2";
        var doc = MarkdownParser.Parse(md);

        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        var item0 = list.Items[0];

        var level1List = item0.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.NotNull(level1List);

        var level1Item = level1List.Items[0];
        var level2List = level1Item.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.NotNull(level2List);
        Assert.Single(level2List.Items);
    }

    [Fact]
    public void Parse_NestedOrderedInUnordered()
    {
        var md = "- Item\n  1. First\n  2. Second";
        var doc = MarkdownParser.Parse(md);

        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        var item = list.Items[0];

        var orderedList = item.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.NotNull(orderedList);
        Assert.True(orderedList.IsOrdered);
        Assert.Equal(2, orderedList.Items.Count);
    }

    // ==========================================================================
    // Strikethrough parsing
    // ==========================================================================

    [Fact]
    public void Parse_Strikethrough_ProducesStrikethroughInline()
    {
        var doc = MarkdownParser.Parse("~~deleted~~");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var strike = Assert.IsType<StrikethroughInline>(Assert.Single(para.Inlines));
        var text = Assert.IsType<TextInline>(Assert.Single(strike.Children));
        Assert.Equal("deleted", text.Text);
    }

    [Fact]
    public void Parse_StrikethroughInSentence_PreservesContext()
    {
        var doc = MarkdownParser.Parse("This is ~~old~~ text");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(3, para.Inlines.Count);
        Assert.IsType<TextInline>(para.Inlines[0]);
        Assert.IsType<StrikethroughInline>(para.Inlines[1]);
        Assert.IsType<TextInline>(para.Inlines[2]);
    }

    [Fact]
    public void Parse_SingleTilde_NotStrikethrough()
    {
        var doc = MarkdownParser.Parse("~not strike~");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        // Should be plain text, not strikethrough
        Assert.All(para.Inlines, i => Assert.IsType<TextInline>(i));
    }

    [Fact]
    public void Parse_StrikethroughWithBold_NestsCorrectly()
    {
        var doc = MarkdownParser.Parse("~~**both**~~");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var strike = Assert.IsType<StrikethroughInline>(Assert.Single(para.Inlines));
        var emphasis = Assert.IsType<EmphasisInline>(Assert.Single(strike.Children));
        Assert.True(emphasis.IsStrong);
    }

    // ==========================================================================
    // Task list parsing
    // ==========================================================================

    [Fact]
    public void Parse_UncheckedTaskItem_SetsIsCheckedFalse()
    {
        var doc = MarkdownParser.Parse("- [ ] Todo item");
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        var item = Assert.Single(list.Items);
        Assert.False(item.IsChecked);
        // The checkbox prefix should be stripped from the text
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(item.Children));
        var text = Assert.IsType<TextInline>(Assert.Single(para.Inlines));
        Assert.Equal("Todo item", text.Text);
    }

    [Fact]
    public void Parse_CheckedTaskItem_SetsIsCheckedTrue()
    {
        var doc = MarkdownParser.Parse("- [x] Done item");
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        var item = Assert.Single(list.Items);
        Assert.True(item.IsChecked);
    }

    [Fact]
    public void Parse_CheckedUppercaseX_SetsIsCheckedTrue()
    {
        var doc = MarkdownParser.Parse("- [X] Also done");
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.True(list.Items[0].IsChecked);
    }

    [Fact]
    public void Parse_NormalListItem_HasNullIsChecked()
    {
        var doc = MarkdownParser.Parse("- Normal item");
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.Null(list.Items[0].IsChecked);
    }

    [Fact]
    public void Parse_MixedTaskAndNormalList()
    {
        var doc = MarkdownParser.Parse("- [x] Done\n- [ ] Todo\n- Normal");
        var list = Assert.IsType<ListBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(3, list.Items.Count);
        Assert.True(list.Items[0].IsChecked);
        Assert.False(list.Items[1].IsChecked);
        Assert.Null(list.Items[2].IsChecked);
    }

    // ==========================================================================
    // GFM Table parsing
    // ==========================================================================

    [Fact]
    public void Parse_SimpleTable_ProducesTableBlock()
    {
        var doc = MarkdownParser.Parse("| A | B |\n|---|---|\n| 1 | 2 |");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(2, table.Alignments.Count);
        Assert.Equal(2, table.HeaderCells.Count);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void Parse_TableAlignment_DetectsCorrectly()
    {
        var doc = MarkdownParser.Parse("| L | C | R | N |\n|:---|:---:|---:|---|\n| a | b | c | d |");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(TableColumnAlignment.Left, table.Alignments[0]);
        Assert.Equal(TableColumnAlignment.Center, table.Alignments[1]);
        Assert.Equal(TableColumnAlignment.Right, table.Alignments[2]);
        Assert.Equal(TableColumnAlignment.None, table.Alignments[3]);
    }

    [Fact]
    public void Parse_TableHeaderContent_ParsesInlines()
    {
        var doc = MarkdownParser.Parse("| **Bold** | `code` |\n|---|---|\n| x | y |");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.IsType<EmphasisInline>(table.HeaderCells[0][0]);
        Assert.IsType<CodeInline>(table.HeaderCells[1][0]);
    }

    [Fact]
    public void Parse_TableMultipleRows()
    {
        var doc = MarkdownParser.Parse("| H |\n|---|\n| A |\n| B |\n| C |");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(3, table.Rows.Count);
    }

    [Fact]
    public void Parse_TableEndedByBlankLine()
    {
        var doc = MarkdownParser.Parse("| H |\n|---|\n| A |\n\nParagraph");
        Assert.Equal(2, doc.Blocks.Count);
        Assert.IsType<TableBlock>(doc.Blocks[0]);
        Assert.IsType<ParagraphBlock>(doc.Blocks[1]);
    }

    [Fact]
    public void Parse_TableWithoutLeadingPipes()
    {
        var doc = MarkdownParser.Parse("A | B\n---|---\n1 | 2");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(2, table.Alignments.Count);
    }

    [Fact]
    public void Parse_TablePadsShortRows()
    {
        var doc = MarkdownParser.Parse("| A | B | C |\n|---|---|---|\n| 1 |");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(3, table.Rows[0].Count); // padded to 3
    }

    [Fact]
    public void Parse_NotATable_MissingDelimiter()
    {
        var doc = MarkdownParser.Parse("| A | B |\n| 1 | 2 |");
        // Without delimiter row, this should be paragraphs, not a table
        Assert.DoesNotContain(doc.Blocks, b => b is TableBlock);
    }

    [Fact]
    public void Parse_TableWithEscapedPipe()
    {
        var doc = MarkdownParser.Parse("| A |\n|---|\n| a\\|b |");
        var table = Assert.IsType<TableBlock>(Assert.Single(doc.Blocks));
        var cellInlines = table.Rows[0][0];
        var text = Assert.IsType<TextInline>(Assert.Single(cellInlines));
        Assert.Equal("a|b", text.Text);
    }

    // ==========================================================================
    // Reference-style links
    // ==========================================================================

    [Fact]
    public void Parse_ReferenceLink_ResolvesToLinkInline()
    {
        var doc = MarkdownParser.Parse("Click [here][link1]\n\n[link1]: https://example.com");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        Assert.Equal(2, para.Inlines.Count);
        var link = Assert.IsType<LinkInline>(para.Inlines[1]);
        Assert.Equal("here", link.Text);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_CaseInsensitive()
    {
        var doc = MarkdownParser.Parse("[Click][LINK]\n\n[link]: https://example.com");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(para.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_CollapsedForm()
    {
        // [text][] means ref = text
        var doc = MarkdownParser.Parse("[example][]\n\n[example]: https://example.com");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(para.Inlines[0]);
        Assert.Equal("example", link.Text);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_ShortcutForm()
    {
        // [text] with no following brackets
        var doc = MarkdownParser.Parse("[example]\n\n[example]: https://example.com");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(para.Inlines[0]);
        Assert.Equal("example", link.Text);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_WithTitle()
    {
        var doc = MarkdownParser.Parse("[click][ref]\n\n[ref]: https://example.com \"My Title\"");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(para.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("My Title", link.Title);
    }

    [Fact]
    public void Parse_ReferenceLink_DefinitionNotRendered()
    {
        var doc = MarkdownParser.Parse("Text\n\n[ref]: https://example.com");
        // Only the paragraph should remain; definition is consumed
        Assert.Single(doc.Blocks);
        Assert.IsType<ParagraphBlock>(doc.Blocks[0]);
    }

    [Fact]
    public void Parse_ReferenceLink_MultipleDefinitions()
    {
        var doc = MarkdownParser.Parse(
            "[a][ref1] and [b][ref2]\n\n[ref1]: https://one.com\n[ref2]: https://two.com");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link1 = Assert.IsType<LinkInline>(para.Inlines[0]);
        var link2 = Assert.IsType<LinkInline>(para.Inlines[2]);
        Assert.Equal("https://one.com", link1.Url);
        Assert.Equal("https://two.com", link2.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_UndefinedRef_NotParsedAsLink()
    {
        var doc = MarkdownParser.Parse("[text][undefined]");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        // Should NOT produce a LinkInline — treated as plain text
        Assert.DoesNotContain(para.Inlines, i => i is LinkInline);
    }

    [Fact]
    public void Parse_ReferenceLink_FirstDefinitionWins()
    {
        var doc = MarkdownParser.Parse("[click][ref]\n\n[ref]: https://first.com\n[ref]: https://second.com");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(para.Inlines[0]);
        Assert.Equal("https://first.com", link.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_AngleBracketUrl()
    {
        var doc = MarkdownParser.Parse("[click][ref]\n\n[ref]: <https://example.com>");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(para.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void Parse_ReferenceImage_ResolvesToImageInline()
    {
        var doc = MarkdownParser.Parse("![logo][img]\n\n[img]: https://example.com/logo.png");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(doc.Blocks));
        var image = Assert.IsType<ImageInline>(para.Inlines[0]);
        Assert.Equal("logo", image.AltText);
        Assert.Equal("https://example.com/logo.png", image.Url);
    }

    [Fact]
    public void Parse_ReferenceLink_InHeading()
    {
        var doc = MarkdownParser.Parse("# [Click here][ref]\n\n[ref]: https://example.com");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(doc.Blocks));
        var link = Assert.IsType<LinkInline>(heading.Inlines[0]);
        Assert.Equal("https://example.com", link.Url);
    }

    [Fact]
    public void Parse_LinkDefinitions_StoredOnDocument()
    {
        var doc = MarkdownParser.Parse("[ref]: https://example.com \"Title\"");
        Assert.True(doc.LinkDefinitions.ContainsKey("ref"));
        Assert.Equal("https://example.com", doc.LinkDefinitions["ref"].Url);
        Assert.Equal("Title", doc.LinkDefinitions["ref"].Title);
    }
}
