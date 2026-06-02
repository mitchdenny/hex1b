using Hex1b.Markdown;

namespace Hex1b.Tests;

[TestClass]
public class MarkdownParserTests
{
    // --- Empty / minimal input ---

    [TestMethod]
    public void Parse_EmptyString_ReturnsEmptyDocument()
    {
        var doc = MarkdownParser.Parse("");
        Assert.IsEmpty(doc.Blocks);
    }

    [TestMethod]
    public void Parse_NullString_ReturnsEmptyDocument()
    {
        var doc = MarkdownParser.Parse((string)null!);
        Assert.IsEmpty(doc.Blocks);
    }

    [TestMethod]
    public void Parse_BlankLines_ReturnsEmptyDocument()
    {
        var doc = MarkdownParser.Parse("\n\n\n");
        Assert.IsEmpty(doc.Blocks);
    }

    // --- Headings ---

    [TestMethod]
    [DataRow("# H1", 1, "H1")]
    [DataRow("## H2", 2, "H2")]
    [DataRow("### H3", 3, "H3")]
    [DataRow("#### H4", 4, "H4")]
    [DataRow("##### H5", 5, "H5")]
    [DataRow("###### H6", 6, "H6")]
    public void Parse_Heading_ReturnsCorrectLevel(string input, int expectedLevel, string expectedText)
    {
        var doc = MarkdownParser.Parse(input);

        var heading = TestSeq.Single(doc.Blocks);
        var h = TestSeq.IsType<HeadingBlock>(heading);
        Assert.AreEqual(expectedLevel, h.Level);
        Assert.AreEqual(expectedText, h.Text);
    }

    [TestMethod]
    public void Parse_HeadingWithClosingHashes_StripsTrailingHashes()
    {
        var doc = MarkdownParser.Parse("## Hello ##");
        var h = TestSeq.IsType<HeadingBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("Hello", h.Text);
    }

    [TestMethod]
    public void Parse_HeadingNoSpace_NotAHeading()
    {
        var doc = MarkdownParser.Parse("#NotAHeading");
        var block = TestSeq.Single(doc.Blocks);
        TestSeq.IsType<ParagraphBlock>(block);
    }

    // --- Paragraphs ---

    [TestMethod]
    public void Parse_SimpleParagraph_ReturnsParagraphBlock()
    {
        var doc = MarkdownParser.Parse("Hello world");
        var p = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("Hello world", p.Text);
    }

    [TestMethod]
    public void Parse_MultiLineParagraph_JoinsLines()
    {
        var doc = MarkdownParser.Parse("Line one\nLine two");
        var p = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("Line one Line two", p.Text);
    }

    [TestMethod]
    public void Parse_TwoParagraphs_SeparatedByBlankLine()
    {
        var doc = MarkdownParser.Parse("First para\n\nSecond para");
        Assert.AreEqual(2, doc.Blocks.Count);
        TestSeq.IsType<ParagraphBlock>(doc.Blocks[0]);
        TestSeq.IsType<ParagraphBlock>(doc.Blocks[1]);
    }

    // --- Fenced Code Blocks ---

    [TestMethod]
    public void Parse_FencedCodeBlock_Backticks()
    {
        var input = "```\ncode here\n```";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<FencedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("", code.Language);
        Assert.AreEqual("code here", code.Content);
    }

    [TestMethod]
    public void Parse_FencedCodeBlock_WithLanguage()
    {
        var input = "```csharp\nvar x = 1;\n```";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<FencedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("csharp", code.Language);
        Assert.AreEqual("var x = 1;", code.Content);
    }

    [TestMethod]
    public void Parse_FencedCodeBlock_Tildes()
    {
        var input = "~~~\ncode\n~~~";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<FencedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("code", code.Content);
    }

    [TestMethod]
    public void Parse_FencedCodeBlock_MultipleLines()
    {
        var input = "```\nline 1\nline 2\nline 3\n```";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<FencedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("line 1\nline 2\nline 3", code.Content);
    }

    [TestMethod]
    public void Parse_FencedCodeBlock_UnclosedTreatsRestAsCode()
    {
        var input = "```\ncode\nmore code";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<FencedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("code\nmore code", code.Content);
    }

    // --- Indented Code Blocks ---

    [TestMethod]
    public void Parse_IndentedCodeBlock_FourSpaces()
    {
        var input = "    code line";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<IndentedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("code line", code.Content);
    }

    [TestMethod]
    public void Parse_IndentedCodeBlock_MultipleLines()
    {
        var input = "    line 1\n    line 2";
        var doc = MarkdownParser.Parse(input);
        var code = TestSeq.IsType<IndentedCodeBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("line 1\nline 2", code.Content);
    }

    // --- Block Quotes ---

    [TestMethod]
    public void Parse_BlockQuote_Simple()
    {
        var input = "> Hello";
        var doc = MarkdownParser.Parse(input);
        var bq = TestSeq.IsType<BlockQuoteBlock>(TestSeq.Single(doc.Blocks));
        var p = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(bq.Children));
        Assert.AreEqual("Hello", p.Text);
    }

    [TestMethod]
    public void Parse_BlockQuote_MultipleLines()
    {
        var input = "> Line 1\n> Line 2";
        var doc = MarkdownParser.Parse(input);
        var bq = TestSeq.IsType<BlockQuoteBlock>(TestSeq.Single(doc.Blocks));
        var p = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(bq.Children));
        Assert.AreEqual("Line 1 Line 2", p.Text);
    }

    [TestMethod]
    public void Parse_BlockQuote_Nested()
    {
        var input = "> > Nested";
        var doc = MarkdownParser.Parse(input);
        var outer = TestSeq.IsType<BlockQuoteBlock>(TestSeq.Single(doc.Blocks));
        var inner = TestSeq.IsType<BlockQuoteBlock>(TestSeq.Single(outer.Children));
        var p = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(inner.Children));
        Assert.AreEqual("Nested", p.Text);
    }

    // --- Unordered Lists ---

    [TestMethod]
    public void Parse_UnorderedList_Dashes()
    {
        var input = "- Item 1\n- Item 2\n- Item 3";
        var doc = MarkdownParser.Parse(input);
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.IsFalse(list.IsOrdered);
        Assert.AreEqual(3, list.Items.Count);
    }

    [TestMethod]
    public void Parse_UnorderedList_Asterisks()
    {
        var input = "* Item 1\n* Item 2";
        var doc = MarkdownParser.Parse(input);
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.IsFalse(list.IsOrdered);
        Assert.AreEqual(2, list.Items.Count);
    }

    [TestMethod]
    public void Parse_UnorderedList_ItemContent()
    {
        var input = "- Hello world";
        var doc = MarkdownParser.Parse(input);
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        var item = TestSeq.Single(list.Items);
        var p = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(item.Children));
        Assert.AreEqual("Hello world", p.Text);
    }

    // --- Ordered Lists ---

    [TestMethod]
    public void Parse_OrderedList()
    {
        var input = "1. First\n2. Second\n3. Third";
        var doc = MarkdownParser.Parse(input);
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.IsTrue(list.IsOrdered);
        Assert.AreEqual(1, list.StartNumber);
        Assert.AreEqual(3, list.Items.Count);
    }

    [TestMethod]
    public void Parse_OrderedList_CustomStartNumber()
    {
        var input = "3. Third\n4. Fourth";
        var doc = MarkdownParser.Parse(input);
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.IsTrue(list.IsOrdered);
        Assert.AreEqual(3, list.StartNumber);
    }

    // --- Thematic Breaks ---

    [TestMethod]
    [DataRow("---")]
    [DataRow("***")]
    [DataRow("___")]
    [DataRow("- - -")]
    [DataRow("* * *")]
    [DataRow("____")]
    public void Parse_ThematicBreak(string input)
    {
        var doc = MarkdownParser.Parse(input);
        TestSeq.IsType<ThematicBreakBlock>(TestSeq.Single(doc.Blocks));
    }

    [TestMethod]
    public void Parse_ThematicBreak_TwoCharsNotEnough()
    {
        var doc = MarkdownParser.Parse("--");
        Assert.IsNotInstanceOfType<ThematicBreakBlock>(TestSeq.Single(doc.Blocks));
    }

    // --- Inline Parsing ---

    [TestMethod]
    public void ParseInlines_PlainText()
    {
        var inlines = MarkdownParser.ParseInlines("Hello world");
        var text = TestSeq.IsType<TextInline>(TestSeq.Single(inlines));
        Assert.AreEqual("Hello world", text.Text);
    }

    [TestMethod]
    public void ParseInlines_Bold()
    {
        var inlines = MarkdownParser.ParseInlines("**bold**");
        var em = TestSeq.IsType<EmphasisInline>(TestSeq.Single(inlines));
        Assert.IsTrue(em.IsStrong);
        var text = TestSeq.IsType<TextInline>(TestSeq.Single(em.Children));
        Assert.AreEqual("bold", text.Text);
    }

    [TestMethod]
    public void ParseInlines_Italic()
    {
        var inlines = MarkdownParser.ParseInlines("*italic*");
        var em = TestSeq.IsType<EmphasisInline>(TestSeq.Single(inlines));
        Assert.IsFalse(em.IsStrong);
        var text = TestSeq.IsType<TextInline>(TestSeq.Single(em.Children));
        Assert.AreEqual("italic", text.Text);
    }

    [TestMethod]
    public void ParseInlines_CodeSpan()
    {
        var inlines = MarkdownParser.ParseInlines("`code`");
        var code = TestSeq.IsType<CodeInline>(TestSeq.Single(inlines));
        Assert.AreEqual("code", code.Code);
    }

    [TestMethod]
    public void ParseInlines_Link()
    {
        var inlines = MarkdownParser.ParseInlines("[click](https://example.com)");
        var link = TestSeq.IsType<LinkInline>(TestSeq.Single(inlines));
        Assert.AreEqual("click", link.Text);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void ParseInlines_LinkWithTitle()
    {
        var inlines = MarkdownParser.ParseInlines("[click](https://example.com \"Title\")");
        var link = TestSeq.IsType<LinkInline>(TestSeq.Single(inlines));
        Assert.AreEqual("click", link.Text);
        Assert.AreEqual("https://example.com", link.Url);
        Assert.AreEqual("Title", link.Title);
    }

    [TestMethod]
    public void ParseInlines_Image()
    {
        var inlines = MarkdownParser.ParseInlines("![alt text](image.png)");
        var image = TestSeq.IsType<ImageInline>(TestSeq.Single(inlines));
        Assert.AreEqual("alt text", image.AltText);
        Assert.AreEqual("image.png", image.Url);
    }

    [TestMethod]
    public void ParseInlines_MixedContent()
    {
        var inlines = MarkdownParser.ParseInlines("Hello **bold** and `code` here");
        Assert.AreEqual(5, inlines.Count);
        TestSeq.IsType<TextInline>(inlines[0]);
        TestSeq.IsType<EmphasisInline>(inlines[1]);
        TestSeq.IsType<TextInline>(inlines[2]);
        TestSeq.IsType<CodeInline>(inlines[3]);
        TestSeq.IsType<TextInline>(inlines[4]);
    }

    // --- Complex documents ---

    [TestMethod]
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
        TestSeq.IsType<HeadingBlock>(doc.Blocks[0]);
        TestSeq.IsType<ParagraphBlock>(doc.Blocks[1]);
        TestSeq.IsType<FencedCodeBlock>(doc.Blocks[2]);
        TestSeq.IsType<BlockQuoteBlock>(doc.Blocks[3]);
        TestSeq.IsType<ListBlock>(doc.Blocks[4]);
        TestSeq.IsType<ListBlock>(doc.Blocks[5]);
        TestSeq.IsType<ThematicBreakBlock>(doc.Blocks[6]);
        TestSeq.IsType<ParagraphBlock>(doc.Blocks[7]);
    }

    [TestMethod]
    public void Parse_BlockQuoteWithCode_NestedCorrectly()
    {
        var input = "> ```\n> code\n> ```";
        var doc = MarkdownParser.Parse(input);
        var bq = TestSeq.IsType<BlockQuoteBlock>(TestSeq.Single(doc.Blocks));
        TestSeq.IsType<FencedCodeBlock>(TestSeq.Single(bq.Children));
    }

    // --- ReadOnlyMemory overload ---

    [TestMethod]
    public void Parse_ReadOnlyMemory_ProducesEquivalentResult()
    {
        var source = "# Hello\n\nWorld";
        var memory = source.AsMemory();

        var docFromString = MarkdownParser.Parse(source);
        var docFromMemory = MarkdownParser.Parse(memory);

        Assert.AreEqual(docFromString.Blocks.Count, docFromMemory.Blocks.Count);
    }

    // --- Edge cases ---

    [TestMethod]
    public void Parse_NoTrailingNewline_HandledCorrectly()
    {
        var doc = MarkdownParser.Parse("# Hello");
        var h = TestSeq.IsType<HeadingBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual("Hello", h.Text);
    }

    [TestMethod]
    public void Parse_OnlyWhitespace_ReturnsEmpty()
    {
        var doc = MarkdownParser.Parse("   \n   \n   ");
        Assert.IsEmpty(doc.Blocks);
    }

    // --- Diagnostic: nested list parsing ---

    [TestMethod]
    public void Parse_NestedList_ProducesNestedStructure()
    {
        var md = "- Item 1\n  - Nested A\n  - Nested B\n- Item 2";
        var doc = MarkdownParser.Parse(md);

        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(2, list.Items.Count);

        // Item 1 should have a paragraph + nested list
        var item1 = list.Items[0];
        Assert.IsTrue(item1.Children.Count >= 2, $"Expected paragraph + nested list, got {item1.Children.Count} children: " +
            string.Join(", ", item1.Children.Select(c => c.GetType().Name)));

        var nestedList = item1.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.IsNotNull(nestedList);
        Assert.AreEqual(2, nestedList.Items.Count);
    }

    [TestMethod]
    public void Parse_NestedList_ThreeLevels()
    {
        var md = "- Level 0\n  - Level 1\n    - Level 2";
        var doc = MarkdownParser.Parse(md);

        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        var item0 = list.Items[0];

        var level1List = item0.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.IsNotNull(level1List);

        var level1Item = level1List.Items[0];
        var level2List = level1Item.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.IsNotNull(level2List);
        TestSeq.Single(level2List.Items);
    }

    [TestMethod]
    public void Parse_NestedOrderedInUnordered()
    {
        var md = "- Item\n  1. First\n  2. Second";
        var doc = MarkdownParser.Parse(md);

        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        var item = list.Items[0];

        var orderedList = item.Children.OfType<ListBlock>().FirstOrDefault();
        Assert.IsNotNull(orderedList);
        Assert.IsTrue(orderedList.IsOrdered);
        Assert.AreEqual(2, orderedList.Items.Count);
    }

    // ==========================================================================
    // Strikethrough parsing
    // ==========================================================================

    [TestMethod]
    public void Parse_Strikethrough_ProducesStrikethroughInline()
    {
        var doc = MarkdownParser.Parse("~~deleted~~");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var strike = TestSeq.IsType<StrikethroughInline>(TestSeq.Single(para.Inlines));
        var text = TestSeq.IsType<TextInline>(TestSeq.Single(strike.Children));
        Assert.AreEqual("deleted", text.Text);
    }

    [TestMethod]
    public void Parse_StrikethroughInSentence_PreservesContext()
    {
        var doc = MarkdownParser.Parse("This is ~~old~~ text");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(3, para.Inlines.Count);
        TestSeq.IsType<TextInline>(para.Inlines[0]);
        TestSeq.IsType<StrikethroughInline>(para.Inlines[1]);
        TestSeq.IsType<TextInline>(para.Inlines[2]);
    }

    [TestMethod]
    public void Parse_SingleTilde_NotStrikethrough()
    {
        var doc = MarkdownParser.Parse("~not strike~");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        // Should be plain text, not strikethrough
        TestSeq.All(para.Inlines, i => TestSeq.IsType<TextInline>(i));
    }

    [TestMethod]
    public void Parse_StrikethroughWithBold_NestsCorrectly()
    {
        var doc = MarkdownParser.Parse("~~**both**~~");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var strike = TestSeq.IsType<StrikethroughInline>(TestSeq.Single(para.Inlines));
        var emphasis = TestSeq.IsType<EmphasisInline>(TestSeq.Single(strike.Children));
        Assert.IsTrue(emphasis.IsStrong);
    }

    // ==========================================================================
    // Task list parsing
    // ==========================================================================

    [TestMethod]
    public void Parse_UncheckedTaskItem_SetsIsCheckedFalse()
    {
        var doc = MarkdownParser.Parse("- [ ] Todo item");
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        var item = TestSeq.Single(list.Items);
        Assert.IsFalse(item.IsChecked);
        // The checkbox prefix should be stripped from the text
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(item.Children));
        var text = TestSeq.IsType<TextInline>(TestSeq.Single(para.Inlines));
        Assert.AreEqual("Todo item", text.Text);
    }

    [TestMethod]
    public void Parse_CheckedTaskItem_SetsIsCheckedTrue()
    {
        var doc = MarkdownParser.Parse("- [x] Done item");
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        var item = TestSeq.Single(list.Items);
        Assert.IsTrue(item.IsChecked);
    }

    [TestMethod]
    public void Parse_CheckedUppercaseX_SetsIsCheckedTrue()
    {
        var doc = MarkdownParser.Parse("- [X] Also done");
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.IsTrue(list.Items[0].IsChecked);
    }

    [TestMethod]
    public void Parse_NormalListItem_HasNullIsChecked()
    {
        var doc = MarkdownParser.Parse("- Normal item");
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.IsNull(list.Items[0].IsChecked);
    }

    [TestMethod]
    public void Parse_MixedTaskAndNormalList()
    {
        var doc = MarkdownParser.Parse("- [x] Done\n- [ ] Todo\n- Normal");
        var list = TestSeq.IsType<ListBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(3, list.Items.Count);
        Assert.IsTrue(list.Items[0].IsChecked);
        Assert.IsFalse(list.Items[1].IsChecked);
        Assert.IsNull(list.Items[2].IsChecked);
    }

    // ==========================================================================
    // GFM Table parsing
    // ==========================================================================

    [TestMethod]
    public void Parse_SimpleTable_ProducesTableBlock()
    {
        var doc = MarkdownParser.Parse("| A | B |\n|---|---|\n| 1 | 2 |");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(2, table.Alignments.Count);
        Assert.AreEqual(2, table.HeaderCells.Count);
        TestSeq.Single(table.Rows);
    }

    [TestMethod]
    public void Parse_TableAlignment_DetectsCorrectly()
    {
        var doc = MarkdownParser.Parse("| L | C | R | N |\n|:---|:---:|---:|---|\n| a | b | c | d |");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(TableColumnAlignment.Left, table.Alignments[0]);
        Assert.AreEqual(TableColumnAlignment.Center, table.Alignments[1]);
        Assert.AreEqual(TableColumnAlignment.Right, table.Alignments[2]);
        Assert.AreEqual(TableColumnAlignment.None, table.Alignments[3]);
    }

    [TestMethod]
    public void Parse_TableHeaderContent_ParsesInlines()
    {
        var doc = MarkdownParser.Parse("| **Bold** | `code` |\n|---|---|\n| x | y |");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        TestSeq.IsType<EmphasisInline>(table.HeaderCells[0][0]);
        TestSeq.IsType<CodeInline>(table.HeaderCells[1][0]);
    }

    [TestMethod]
    public void Parse_TableMultipleRows()
    {
        var doc = MarkdownParser.Parse("| H |\n|---|\n| A |\n| B |\n| C |");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(3, table.Rows.Count);
    }

    [TestMethod]
    public void Parse_TableEndedByBlankLine()
    {
        var doc = MarkdownParser.Parse("| H |\n|---|\n| A |\n\nParagraph");
        Assert.AreEqual(2, doc.Blocks.Count);
        TestSeq.IsType<TableBlock>(doc.Blocks[0]);
        TestSeq.IsType<ParagraphBlock>(doc.Blocks[1]);
    }

    [TestMethod]
    public void Parse_TableWithoutLeadingPipes()
    {
        var doc = MarkdownParser.Parse("A | B\n---|---\n1 | 2");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(2, table.Alignments.Count);
    }

    [TestMethod]
    public void Parse_TablePadsShortRows()
    {
        var doc = MarkdownParser.Parse("| A | B | C |\n|---|---|---|\n| 1 |");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(3, table.Rows[0].Count); // padded to 3
    }

    [TestMethod]
    public void Parse_NotATable_MissingDelimiter()
    {
        var doc = MarkdownParser.Parse("| A | B |\n| 1 | 2 |");
        // Without delimiter row, this should be paragraphs, not a table
        Assert.IsFalse(doc.Blocks.Any(b => b is TableBlock));
    }

    [TestMethod]
    public void Parse_TableWithEscapedPipe()
    {
        var doc = MarkdownParser.Parse("| A |\n|---|\n| a\\|b |");
        var table = TestSeq.IsType<TableBlock>(TestSeq.Single(doc.Blocks));
        var cellInlines = table.Rows[0][0];
        var text = TestSeq.IsType<TextInline>(TestSeq.Single(cellInlines));
        Assert.AreEqual("a|b", text.Text);
    }

    // ==========================================================================
    // Reference-style links
    // ==========================================================================

    [TestMethod]
    public void Parse_ReferenceLink_ResolvesToLinkInline()
    {
        var doc = MarkdownParser.Parse("Click [here][link1]\n\n[link1]: https://example.com");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        Assert.AreEqual(2, para.Inlines.Count);
        var link = TestSeq.IsType<LinkInline>(para.Inlines[1]);
        Assert.AreEqual("here", link.Text);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_CaseInsensitive()
    {
        var doc = MarkdownParser.Parse("[Click][LINK]\n\n[link]: https://example.com");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_CollapsedForm()
    {
        // [text][] means ref = text
        var doc = MarkdownParser.Parse("[example][]\n\n[example]: https://example.com");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        Assert.AreEqual("example", link.Text);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_ShortcutForm()
    {
        // [text] with no following brackets
        var doc = MarkdownParser.Parse("[example]\n\n[example]: https://example.com");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        Assert.AreEqual("example", link.Text);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_WithTitle()
    {
        var doc = MarkdownParser.Parse("[click][ref]\n\n[ref]: https://example.com \"My Title\"");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        Assert.AreEqual("https://example.com", link.Url);
        Assert.AreEqual("My Title", link.Title);
    }

    [TestMethod]
    public void Parse_ReferenceLink_DefinitionNotRendered()
    {
        var doc = MarkdownParser.Parse("Text\n\n[ref]: https://example.com");
        // Only the paragraph should remain; definition is consumed
        TestSeq.Single(doc.Blocks);
        TestSeq.IsType<ParagraphBlock>(doc.Blocks[0]);
    }

    [TestMethod]
    public void Parse_ReferenceLink_MultipleDefinitions()
    {
        var doc = MarkdownParser.Parse(
            "[a][ref1] and [b][ref2]\n\n[ref1]: https://one.com\n[ref2]: https://two.com");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link1 = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        var link2 = TestSeq.IsType<LinkInline>(para.Inlines[2]);
        Assert.AreEqual("https://one.com", link1.Url);
        Assert.AreEqual("https://two.com", link2.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_UndefinedRef_NotParsedAsLink()
    {
        var doc = MarkdownParser.Parse("[text][undefined]");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        // Should NOT produce a LinkInline — treated as plain text
        Assert.IsFalse(para.Inlines.Any(i => i is LinkInline));
    }

    [TestMethod]
    public void Parse_ReferenceLink_FirstDefinitionWins()
    {
        var doc = MarkdownParser.Parse("[click][ref]\n\n[ref]: https://first.com\n[ref]: https://second.com");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        Assert.AreEqual("https://first.com", link.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_AngleBracketUrl()
    {
        var doc = MarkdownParser.Parse("[click][ref]\n\n[ref]: <https://example.com>");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(para.Inlines[0]);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void Parse_ReferenceImage_ResolvesToImageInline()
    {
        var doc = MarkdownParser.Parse("![logo][img]\n\n[img]: https://example.com/logo.png");
        var para = TestSeq.IsType<ParagraphBlock>(TestSeq.Single(doc.Blocks));
        var image = TestSeq.IsType<ImageInline>(para.Inlines[0]);
        Assert.AreEqual("logo", image.AltText);
        Assert.AreEqual("https://example.com/logo.png", image.Url);
    }

    [TestMethod]
    public void Parse_ReferenceLink_InHeading()
    {
        var doc = MarkdownParser.Parse("# [Click here][ref]\n\n[ref]: https://example.com");
        var heading = TestSeq.IsType<HeadingBlock>(TestSeq.Single(doc.Blocks));
        var link = TestSeq.IsType<LinkInline>(heading.Inlines[0]);
        Assert.AreEqual("https://example.com", link.Url);
    }

    [TestMethod]
    public void Parse_LinkDefinitions_StoredOnDocument()
    {
        var doc = MarkdownParser.Parse("[ref]: https://example.com \"Title\"");
        Assert.IsTrue(doc.LinkDefinitions.ContainsKey("ref"));
        Assert.AreEqual("https://example.com", doc.LinkDefinitions["ref"].Url);
        Assert.AreEqual("Title", doc.LinkDefinitions["ref"].Title);
    }
}
