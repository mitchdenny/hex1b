using Hex1b.Markdown;
using Hex1b.Theming;

namespace Hex1b.Tests;

public class MarkdownInlineRendererTests
{
    // ==========================================================================
    // FlattenInlines
    // ==========================================================================

    [Fact]
    public void FlattenInlines_PlainText_SingleRun()
    {
        var inlines = new MarkdownInline[] { new TextInline("Hello world") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("Hello world", run.Text);
        Assert.Null(run.Foreground);
        Assert.Null(run.Background);
        Assert.Equal(CellAttributes.None, run.Attributes);
    }

    [Fact]
    public void FlattenInlines_Bold_SetsBoldAttribute()
    {
        var inlines = new MarkdownInline[]
        {
            new EmphasisInline(isStrong: true, [new TextInline("bold")])
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("bold", run.Text);
        Assert.True((run.Attributes & CellAttributes.Bold) != 0);
    }

    [Fact]
    public void FlattenInlines_Italic_SetsItalicAttribute()
    {
        var inlines = new MarkdownInline[]
        {
            new EmphasisInline(isStrong: false, [new TextInline("italic")])
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("italic", run.Text);
        Assert.True((run.Attributes & CellAttributes.Italic) != 0);
    }

    [Fact]
    public void FlattenInlines_NestedBoldItalic_ComposesAttributes()
    {
        // ***bold italic***
        var inlines = new MarkdownInline[]
        {
            new EmphasisInline(isStrong: true, [
                new EmphasisInline(isStrong: false, [new TextInline("both")])
            ])
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("both", run.Text);
        Assert.True((run.Attributes & CellAttributes.Bold) != 0);
        Assert.True((run.Attributes & CellAttributes.Italic) != 0);
    }

    [Fact]
    public void FlattenInlines_CodeSpan_SetsColorAndBackground()
    {
        var inlines = new MarkdownInline[] { new CodeInline("x = 0") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("x = 0", run.Text);
        Assert.NotNull(run.Foreground);
        Assert.NotNull(run.Background);
    }

    [Fact]
    public void FlattenInlines_Link_SetsUnderlineColorAndUrl()
    {
        var inlines = new MarkdownInline[] { new LinkInline("click", "https://example.com") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("click", run.Text);
        Assert.True((run.Attributes & CellAttributes.Underline) != 0);
        Assert.NotNull(run.Foreground);
        Assert.Equal("https://example.com", run.Url);
    }

    [Fact]
    public void FlattenInlines_Image_SetsAltTextWithBracketsAndUrl()
    {
        var inlines = new MarkdownInline[] { new ImageInline("logo", "img.png") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        var run = Assert.Single(runs);
        Assert.Equal("[logo]", run.Text);
        Assert.True((run.Attributes & CellAttributes.Italic) != 0);
        Assert.Equal("img.png", run.Url);
    }

    [Fact]
    public void FlattenInlines_HardBreak_EmitsNewline()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("first"),
            new LineBreakInline(isHard: true),
            new TextInline("second")
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Equal(3, runs.Count);
        Assert.Equal("first", runs[0].Text);
        Assert.Equal("\n", runs[1].Text);
        Assert.Equal("second", runs[2].Text);
    }

    [Fact]
    public void FlattenInlines_SoftBreak_EmitsSpace()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("first"),
            new LineBreakInline(isHard: false),
            new TextInline("second")
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Equal(3, runs.Count);
        Assert.Equal(" ", runs[1].Text);
    }

    [Fact]
    public void FlattenInlines_MixedContent_ProducesMultipleRuns()
    {
        // "Hello **bold** and `code` here"
        var inlines = new MarkdownInline[]
        {
            new TextInline("Hello "),
            new EmphasisInline(isStrong: true, [new TextInline("bold")]),
            new TextInline(" and "),
            new CodeInline("code"),
            new TextInline(" here")
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Equal(5, runs.Count);
        Assert.Equal("Hello ", runs[0].Text);
        Assert.Equal("bold", runs[1].Text);
        Assert.True((runs[1].Attributes & CellAttributes.Bold) != 0);
        Assert.Equal(" and ", runs[2].Text);
        Assert.Equal("code", runs[3].Text);
        Assert.NotNull(runs[3].Background); // code has bg
        Assert.Equal(" here", runs[4].Text);
    }

    [Fact]
    public void FlattenInlines_WithBaseAttributes_AppliedToAll()
    {
        var inlines = new MarkdownInline[] { new TextInline("text") };
        var runs = MarkdownInlineRenderer.FlattenInlines(
            inlines, baseAttributes: CellAttributes.Bold);

        var run = Assert.Single(runs);
        Assert.True((run.Attributes & CellAttributes.Bold) != 0);
    }

    [Fact]
    public void FlattenInlines_WithBaseForeground_AppliedToPlainText()
    {
        var fg = Hex1bColor.FromRgb(255, 0, 0);
        var inlines = new MarkdownInline[] { new TextInline("text") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines, baseForeground: fg);

        var run = Assert.Single(runs);
        Assert.NotNull(run.Foreground);
        Assert.Equal(255, run.Foreground!.Value.R);
    }

    // ==========================================================================
    // SplitIntoWords
    // ==========================================================================

    [Fact]
    public void SplitIntoWords_SingleWord_OneStyledWord()
    {
        var runs = new List<MarkdownTextRun>
        {
            new("hello", null, null, CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        var word = Assert.Single(words);
        Assert.Single(word.Fragments);
        Assert.Equal("hello", word.Fragments[0].Text);
        Assert.Equal(5, word.DisplayWidth);
        Assert.False(word.PrecededBySpace);
    }

    [Fact]
    public void SplitIntoWords_TwoWordsInOneRun_TwoStyledWords()
    {
        var runs = new List<MarkdownTextRun>
        {
            new("hello world", null, null, CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        Assert.Equal(2, words.Count);
        Assert.Equal("hello", words[0].Fragments[0].Text);
        Assert.False(words[0].PrecededBySpace);
        Assert.Equal("world", words[1].Fragments[0].Text);
        Assert.True(words[1].PrecededBySpace);
    }

    [Fact]
    public void SplitIntoWords_PartialWordStyling_OneWordMultipleFragments()
    {
        // par**tial**ly → one word with 3 fragments
        var runs = new List<MarkdownTextRun>
        {
            new("par", null, null, CellAttributes.None),
            new("tial", null, null, CellAttributes.Bold),
            new("ly", null, null, CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        var word = Assert.Single(words);
        Assert.Equal(3, word.Fragments.Count);
        Assert.Equal("par", word.Fragments[0].Text);
        Assert.Equal("tial", word.Fragments[1].Text);
        Assert.True((word.Fragments[1].Attributes & CellAttributes.Bold) != 0);
        Assert.Equal("ly", word.Fragments[2].Text);
        Assert.Equal(9, word.DisplayWidth);
    }

    [Fact]
    public void SplitIntoWords_SpaceBetweenRuns_SeparateWords()
    {
        // "Hello **bold world** end"
        var runs = new List<MarkdownTextRun>
        {
            new("Hello ", null, null, CellAttributes.None),
            new("bold world", null, null, CellAttributes.Bold),
            new(" end", null, null, CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        Assert.Equal(4, words.Count);
        Assert.Equal("Hello", words[0].Fragments[0].Text);
        Assert.Equal("bold", words[1].Fragments[0].Text);
        Assert.True((words[1].Fragments[0].Attributes & CellAttributes.Bold) != 0);
        Assert.Equal("world", words[2].Fragments[0].Text);
        Assert.True((words[2].Fragments[0].Attributes & CellAttributes.Bold) != 0);
        Assert.Equal("end", words[3].Fragments[0].Text);
    }

    [Fact]
    public void SplitIntoWords_BoldWordPlusPlainPunctuation_OneWord()
    {
        // Hello **world**!
        var runs = new List<MarkdownTextRun>
        {
            new("Hello ", null, null, CellAttributes.None),
            new("world", null, null, CellAttributes.Bold),
            new("!", null, null, CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        Assert.Equal(2, words.Count);
        Assert.Equal("Hello", words[0].Fragments[0].Text);
        // "world" + "!" grouped into one word
        Assert.Equal(2, words[1].Fragments.Count);
        Assert.Equal("world", words[1].Fragments[0].Text);
        Assert.Equal("!", words[1].Fragments[1].Text);
        Assert.Equal(6, words[1].DisplayWidth);
    }

    [Fact]
    public void SplitIntoWords_CodeSpan_NeverSplit()
    {
        // Code spans have background color and should not be split
        var runs = new List<MarkdownTextRun>
        {
            new("int x = 0", Hex1bColor.FromRgb(220, 170, 120), Hex1bColor.FromRgb(50, 50, 50), CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        var word = Assert.Single(words);
        Assert.Equal("int x = 0", word.Fragments[0].Text);
        Assert.Equal(9, word.DisplayWidth);
    }

    [Fact]
    public void SplitIntoWords_NewlineRun_CreatesNewlineWord()
    {
        var runs = new List<MarkdownTextRun>
        {
            new("first", null, null, CellAttributes.None),
            new("\n", null, null, CellAttributes.None),
            new("second", null, null, CellAttributes.None)
        };
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        Assert.Equal(3, words.Count);
        Assert.Equal("first", words[0].Fragments[0].Text);
        Assert.Equal("\n", words[1].Fragments[0].Text);
        Assert.Equal("second", words[2].Fragments[0].Text);
    }

    [Fact]
    public void SplitIntoWords_EmptyRuns_EmptyResult()
    {
        var runs = new List<MarkdownTextRun>();
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        Assert.Empty(words);
    }

    // ==========================================================================
    // WrapLines
    // ==========================================================================

    [Fact]
    public void WrapLines_ShortText_SingleLine()
    {
        var words = new List<StyledWord>
        {
            new([new MarkdownTextRun("Hello", null, null, CellAttributes.None)], 5, false),
            new([new MarkdownTextRun("world", null, null, CellAttributes.None)], 5, true)
        };
        var lines = MarkdownInlineRenderer.WrapLines(words, 20);

        var line = Assert.Single(lines);
        Assert.Contains("Hello", line);
        Assert.Contains("world", line);
    }

    [Fact]
    public void WrapLines_WrapsAtWordBoundary()
    {
        // "Hello world" at width 8 → "Hello" / "world"
        var words = new List<StyledWord>
        {
            new([new MarkdownTextRun("Hello", null, null, CellAttributes.None)], 5, false),
            new([new MarkdownTextRun("world", null, null, CellAttributes.None)], 5, true)
        };
        var lines = MarkdownInlineRenderer.WrapLines(words, 8);

        Assert.Equal(2, lines.Count);
        Assert.Contains("Hello", lines[0]);
        Assert.Contains("world", lines[1]);
    }

    [Fact]
    public void WrapLines_OversizedWord_CharacterBreaks()
    {
        // "abcdefghij" at width 4 → "abcd" / "efgh" / "ij"
        var words = new List<StyledWord>
        {
            new([new MarkdownTextRun("abcdefghij", null, null, CellAttributes.None)], 10, false)
        };
        var lines = MarkdownInlineRenderer.WrapLines(words, 4);

        Assert.True(lines.Count >= 2);
    }

    [Fact]
    public void WrapLines_BoldText_ContainsAnsiCodes()
    {
        var words = new List<StyledWord>
        {
            new([new MarkdownTextRun("bold", null, null, CellAttributes.Bold)], 4, false)
        };
        var lines = MarkdownInlineRenderer.WrapLines(words, 20);

        var line = Assert.Single(lines);
        Assert.Contains("\x1b[1m", line);  // Bold on
        Assert.Contains("bold", line);
        Assert.Contains("\x1b[0m", line);  // Reset
    }

    [Fact]
    public void WrapLines_MultiFragmentWord_AllFragmentsRendered()
    {
        // par**tial**ly as one word
        var words = new List<StyledWord>
        {
            new([
                new MarkdownTextRun("par", null, null, CellAttributes.None),
                new MarkdownTextRun("tial", null, null, CellAttributes.Bold),
                new MarkdownTextRun("ly", null, null, CellAttributes.None)
            ], 9, false)
        };
        var lines = MarkdownInlineRenderer.WrapLines(words, 20);

        var line = Assert.Single(lines);
        Assert.Contains("par", line);
        Assert.Contains("tial", line);
        Assert.Contains("ly", line);
        Assert.Contains("\x1b[1m", line); // Bold for "tial"
    }

    [Fact]
    public void WrapLines_ExplicitNewline_ForcesLineBreak()
    {
        var words = new List<StyledWord>
        {
            new([new MarkdownTextRun("first", null, null, CellAttributes.None)], 5, false),
            new([new MarkdownTextRun("\n", null, null, CellAttributes.None)], 0, false),
            new([new MarkdownTextRun("second", null, null, CellAttributes.None)], 6, false)
        };
        var lines = MarkdownInlineRenderer.WrapLines(words, 80);

        Assert.Equal(2, lines.Count);
        Assert.Contains("first", lines[0]);
        Assert.Contains("second", lines[1]);
    }

    [Fact]
    public void WrapLines_EmptyInput_SingleEmptyLine()
    {
        var lines = MarkdownInlineRenderer.WrapLines([], 20);
        var line = Assert.Single(lines);
        Assert.Equal("", line);
    }

    // ==========================================================================
    // RenderFragmentsToAnsi
    // ==========================================================================

    [Fact]
    public void RenderFragmentsToAnsi_PlainText_NoAnsi()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("hello", null, null, CellAttributes.None)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_BoldText_WrappedInSgr()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("bold", null, null, CellAttributes.Bold)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.StartsWith("\x1b[1m", result);
        Assert.Contains("bold", result);
        Assert.EndsWith("\x1b[0m", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_ItalicText_WrappedInSgr()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("italic", null, null, CellAttributes.Italic)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.Contains("\x1b[3m", result);
        Assert.Contains("italic", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_BoldAndItalic_BothCodes()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("both", null, null, CellAttributes.Bold | CellAttributes.Italic)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.Contains("\x1b[1m", result); // Bold
        Assert.Contains("\x1b[3m", result); // Italic
    }

    [Fact]
    public void RenderFragmentsToAnsi_ForegroundColor_EmitsRgb()
    {
        var fg = Hex1bColor.FromRgb(255, 128, 0);
        var fragments = new MarkdownTextRun[]
        {
            new("colored", fg, null, CellAttributes.None)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.Contains("\x1b[38;2;255;128;0m", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_BackgroundColor_EmitsRgb()
    {
        var bg = Hex1bColor.FromRgb(50, 50, 50);
        var fragments = new MarkdownTextRun[]
        {
            new("bg", null, bg, CellAttributes.None)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.Contains("\x1b[48;2;50;50;50m", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_AdjacentSameStyle_NoExtraResets()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("hello", null, null, CellAttributes.Bold),
            new(" world", null, null, CellAttributes.Bold)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        // Should contain bold once at start, text, and reset once at end
        Assert.Contains("hello world", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_StyleTransition_ResetsAndReapplies()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("bold", null, null, CellAttributes.Bold),
            new("plain", null, null, CellAttributes.None)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);
        Assert.Contains("bold", result);
        Assert.Contains("plain", result);
        // The reset between bold and plain
        Assert.Contains("\x1b[0m", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_Empty_EmptyString()
    {
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi([]);
        Assert.Equal("", result);
    }

    // ==========================================================================
    // RenderLines (full pipeline)
    // ==========================================================================

    [Fact]
    public void RenderLines_PlainParagraph_SingleLine()
    {
        var inlines = new MarkdownInline[] { new TextInline("Hello world") };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 80);

        var line = Assert.Single(lines);
        Assert.Equal("Hello world", line);
    }

    [Fact]
    public void RenderLines_BoldInParagraph_ContainsAnsi()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("Hello "),
            new EmphasisInline(isStrong: true, [new TextInline("bold")]),
            new TextInline(" world")
        };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 80);

        var line = Assert.Single(lines);
        Assert.Contains("\x1b[1m", line);
        Assert.Contains("bold", line);
    }

    [Fact]
    public void RenderLines_WrappingPreservesStyle()
    {
        // "Hello **bold text** end" at width 12
        // Should wrap: "Hello bold" / "text end"
        // (bold should continue on second line)
        var inlines = new MarkdownInline[]
        {
            new TextInline("Hello "),
            new EmphasisInline(isStrong: true, [new TextInline("bold text")]),
            new TextInline(" end")
        };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 12);

        Assert.True(lines.Count >= 2, $"Expected 2+ lines, got {lines.Count}");
    }

    [Fact]
    public void RenderLines_HeadingWithBaseFg_AppliesColor()
    {
        var fg = Hex1bColor.FromRgb(100, 200, 255);
        var inlines = new MarkdownInline[] { new TextInline("Title") };
        var lines = MarkdownInlineRenderer.RenderLines(
            inlines, 80, baseForeground: fg, baseAttributes: CellAttributes.Bold);

        var line = Assert.Single(lines);
        Assert.Contains("\x1b[1m", line);  // Bold
        Assert.Contains("\x1b[38;2;100;200;255m", line);  // Heading color
    }

    [Fact]
    public void RenderLines_ZeroWidth_EmptyLine()
    {
        var inlines = new MarkdownInline[] { new TextInline("text") };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 0);

        var line = Assert.Single(lines);
        Assert.Equal("", line);
    }

    [Fact]
    public void RenderLines_MultiWordLink_EachWordStyled()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click here", "https://example.com")
        };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 80);

        var line = Assert.Single(lines);
        Assert.Contains("click", line);
        Assert.Contains("here", line);
        Assert.Contains("\x1b[4m", line);  // Underline
    }

    [Fact]
    public void RenderLines_MultiWordLink_SpaceBetweenWordsIsUnderlined()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click here", "https://example.com")
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(words, 80);

        var line = Assert.Single(result.Lines);

        // The space between "click" and "here" should be styled as part of the link.
        // Strip all ANSI escapes (SGR and OSC 8) and verify plain text is "click here"
        var plain = System.Text.RegularExpressions.Regex.Replace(line, @"\x1b(\[[^a-zA-Z]*[a-zA-Z]|\][^\x1b]*\x1b\\)", "");
        Assert.Equal("click here", plain);

        // The OSC 8 hyperlink should NOT be closed and reopened between words.
        // With continuous styling, there's only one OSC 8 open + one close.
        var osc8Start = "\x1b]8;;https://example.com\x1b\\";
        var osc8End = "\x1b]8;;\x1b\\";
        var startCount = CountOccurrences(line, osc8Start);
        var endCount = CountOccurrences(line, osc8End);
        Assert.Equal(1, startCount);
        Assert.Equal(1, endCount);
    }

    [Fact]
    public void WrapLinesWithLinks_MultiWordLink_SpaceHasLinkId()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click here", "https://example.com")
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        // Use a wide width so both words fit on one line
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(words, 80);

        // The link region width should include the space (5 + 1 + 4 = 10)
        var link = Assert.Single(result.LinkRegions);
        Assert.Equal("click here", link.Text);
        Assert.Equal(10, link.DisplayWidth); // "click" + space + "here"
    }

    [Fact]
    public void WrapLinesWithLinks_MultiWordLink_FocusHighlightsSpace()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click here", "https://example.com")
        };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var linkId = runs[0].LinkId;

        // Render with focus on this link
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(words, 80, focusedLinkId: linkId);
        var line = Assert.Single(result.Lines);

        // Bold is applied for focus. There should be no style gap between "click" and "here".
        // Count reset codes — if the space inherits link styling, we won't see
        // a reset+re-apply between the two words.
        var resetCode = "\x1b[0m";

        // Find the area between "click" and "here" in the ANSI output
        var clickIdx = line.IndexOf("click");
        var hereIdx = line.IndexOf("here", clickIdx + 5);
        var between = line[clickIdx..(hereIdx + 4)]; // "click...here" with ANSI

        // Should contain "click here" (with space) and no reset between them
        Assert.DoesNotContain(resetCode, between);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }

    [Fact]
    public void RenderLines_CodeSpanNotSplit()
    {
        // Code span "int x = 0" at width 6 should not split on spaces
        var inlines = new MarkdownInline[] { new CodeInline("int x") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        // Should be a single word (code spans are atomic)
        var word = Assert.Single(words);
        Assert.Equal("int x", word.Fragments[0].Text);
    }

    // ==========================================================================
    // OSC 8 Clickable Links
    // ==========================================================================

    [Fact]
    public void RenderFragmentsToAnsi_LinkFragment_EmitsOsc8()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("click", Hex1bColor.FromRgb(100, 160, 255), null, CellAttributes.Underline, "https://example.com")
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);

        // Should contain OSC 8 start and end sequences
        Assert.Contains("\x1b]8;;https://example.com\x1b\\", result);  // OSC 8 start
        Assert.Contains("click", result);
        Assert.Contains("\x1b]8;;\x1b\\", result);  // OSC 8 end
    }

    [Fact]
    public void RenderFragmentsToAnsi_PlainThenLink_TransitionsOsc8()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("Hello ", null, null, CellAttributes.None),
            new("click", Hex1bColor.FromRgb(100, 160, 255), null, CellAttributes.Underline, "https://example.com"),
            new(" world", null, null, CellAttributes.None)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);

        // OSC 8 should wrap only "click"
        var osc8Start = "\x1b]8;;https://example.com\x1b\\";
        var osc8End = "\x1b]8;;\x1b\\";

        var startIdx = result.IndexOf(osc8Start);
        var endIdx = result.IndexOf(osc8End, startIdx);

        Assert.True(startIdx >= 0, "OSC 8 start sequence not found");
        Assert.True(endIdx > startIdx, "OSC 8 end sequence not found after start");

        // "Hello" should be before OSC 8 start
        var helloIdx = result.IndexOf("Hello");
        Assert.True(helloIdx < startIdx, "Hello should be before OSC 8 start");

        // "world" should be after OSC 8 end
        var worldIdx = result.IndexOf("world");
        Assert.True(worldIdx > endIdx, "world should be after OSC 8 end");
    }

    [Fact]
    public void RenderFragmentsToAnsi_AdjacentLinkFragments_ShareOsc8()
    {
        // Two fragments with same URL should share the OSC 8 wrapper
        var url = "https://example.com";
        var fragments = new MarkdownTextRun[]
        {
            new("click", Hex1bColor.FromRgb(100, 160, 255), null, CellAttributes.Underline, url),
            new("here", Hex1bColor.FromRgb(100, 160, 255), null, CellAttributes.Underline, url)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);

        // Count OSC 8 start sequences — should be exactly 1
        var osc8Start = $"\x1b]8;;{url}\x1b\\";
        var count = 0;
        var idx = 0;
        while ((idx = result.IndexOf(osc8Start, idx)) >= 0)
        {
            count++;
            idx += osc8Start.Length;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void RenderFragmentsToAnsi_DifferentUrls_SeparateOsc8()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("first", null, null, CellAttributes.Underline, "https://first.com"),
            new("second", null, null, CellAttributes.Underline, "https://second.com")
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);

        Assert.Contains("\x1b]8;;https://first.com\x1b\\", result);
        Assert.Contains("\x1b]8;;https://second.com\x1b\\", result);
    }

    [Fact]
    public void RenderFragmentsToAnsi_NoUrl_NoOsc8()
    {
        var fragments = new MarkdownTextRun[]
        {
            new("hello", null, null, CellAttributes.None)
        };
        var result = MarkdownInlineRenderer.RenderFragmentsToAnsi(fragments);

        Assert.DoesNotContain("\x1b]8", result);
    }

    [Fact]
    public void RenderLines_Link_ContainsOsc8()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("Visit "),
            new LinkInline("example", "https://example.com"),
            new TextInline(" now")
        };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 80);

        var line = Assert.Single(lines);
        Assert.Contains("\x1b]8;;https://example.com\x1b\\", line);
        Assert.Contains("example", line);
        Assert.Contains("\x1b]8;;\x1b\\", line);
    }

    [Fact]
    public void RenderLines_MultiWordLink_WrappedLines_EachHasOsc8()
    {
        // "click here" at width 8 should wrap — each line should have its own OSC 8
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click here", "https://example.com")
        };
        var lines = MarkdownInlineRenderer.RenderLines(inlines, 8);

        Assert.Equal(2, lines.Count);
        // Each line should have its own OSC 8 start and end
        foreach (var line in lines)
        {
            Assert.Contains("\x1b]8;;https://example.com\x1b\\", line);
            Assert.Contains("\x1b]8;;\x1b\\", line);
        }
    }

    [Fact]
    public void FlattenInlines_PlainText_NoUrl()
    {
        var inlines = new MarkdownInline[] { new TextInline("hello") };
        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Null(Assert.Single(runs).Url);
    }

    // ==========================================================================
    // HangingIndent
    // ==========================================================================

    [Fact]
    public void WrapLinesWithLinks_HangingIndent_FirstLineFullWidth()
    {
        var runs = MarkdownInlineRenderer.FlattenInlines(
            [new TextInline("• one two three four five")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(words, 15, hangingIndent: 2);
        var lines = result.Lines;

        // Should produce multiple lines
        Assert.True(lines.Count >= 2, $"Expected wrapping, got {lines.Count} lines");

        // First line uses full width
        Assert.True(DisplayWidth.GetStringWidth(lines[0]) <= 15);

        // Continuation lines have 2-char space prefix (after ANSI reset codes)
        // The raw ANSI string may have escape codes before the spaces, but
        // the display output should start with spaces
        for (int i = 1; i < lines.Count; i++)
        {
            var lineWidth = DisplayWidth.GetStringWidth(lines[i]);
            Assert.True(lineWidth <= 15, $"Line {i} exceeds maxWidth: {lineWidth}");
        }
    }

    [Fact]
    public void WrapLinesWithLinks_HangingIndent_ContinuationReducedWidth()
    {
        // "1. " = 3 chars indent; maxWidth=12 → continuation lines use 9 chars
        var runs = MarkdownInlineRenderer.FlattenInlines(
            [new TextInline("1. alpha beta gamma delta")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(words, 12, hangingIndent: 3);
        var lines = result.Lines;

        Assert.True(lines.Count >= 2, $"Expected wrapping, got {lines.Count} lines");

        // Each line should not exceed maxWidth
        foreach (var line in lines)
        {
            var w = DisplayWidth.GetStringWidth(line);
            Assert.True(w <= 12, $"Line exceeds maxWidth 12: width={w}");
        }
    }

    [Fact]
    public void WrapLinesWithLinks_NoHangingIndent_DefaultBehavior()
    {
        var runs = MarkdownInlineRenderer.FlattenInlines([new TextInline("one two three four")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result0 = MarkdownInlineRenderer.WrapLinesWithLinks(words, 10, hangingIndent: 0);
        var resultDefault = MarkdownInlineRenderer.WrapLinesWithLinks(words, 10);

        // With hangingIndent=0, behavior should be identical to default
        Assert.Equal(result0.Lines.Count, resultDefault.Lines.Count);
        for (int i = 0; i < result0.Lines.Count; i++)
        {
            Assert.Equal(result0.Lines[i], resultDefault.Lines[i]);
        }
    }

    [Fact]
    public void WrapLinesWithLinks_HangingIndent_LongFirstWord_CharacterBreaks()
    {
        // "• " (2 chars) + "Accessibility" (13 chars) = 15 > maxWidth 14
        // Should character-break "Accessibility" instead of leaving "• " alone
        var runs = MarkdownInlineRenderer.FlattenInlines(
            [new TextInline("• Accessibility is important")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(words, 14, hangingIndent: 2);
        var lines = result.Lines;

        // First line should have the marker AND some of the word (not marker alone)
        var firstLineWidth = DisplayWidth.GetStringWidth(lines[0]);
        Assert.True(firstLineWidth > 2,
            $"First line should have marker + partial word, not marker alone. Width={firstLineWidth}");

        // All lines fit within maxWidth
        foreach (var line in lines)
        {
            var w = DisplayWidth.GetStringWidth(line);
            Assert.True(w <= 14, $"Line exceeds maxWidth: width={w}");
        }
    }

    // ==========================================================================
    // ContinuationPrefix
    // ==========================================================================

    [Fact]
    public void WrapLinesWithLinks_ContinuationPrefix_AppearsOnAllWrappedLines()
    {
        // Simulate block quote: "│ " prefix on first inline, hanging indent 2, continuation prefix "│ "
        var runs = MarkdownInlineRenderer.FlattenInlines(
            [new TextInline("│ hello world this is a block quote")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(
            words, 15, hangingIndent: 2, continuationPrefix: "│ ");
        var lines = result.Lines;

        Assert.True(lines.Count >= 2, $"Expected wrapping, got {lines.Count} lines");

        // Every line (including continuation) should start with "│ "
        foreach (var line in lines)
        {
            var stripped = StripAnsi(line);
            Assert.StartsWith("│ ", stripped);
        }
    }

    [Fact]
    public void WrapLinesWithLinks_ContinuationPrefix_LinesRespectMaxWidth()
    {
        var runs = MarkdownInlineRenderer.FlattenInlines(
            [new TextInline("│ alpha beta gamma delta epsilon")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(
            words, 12, hangingIndent: 2, continuationPrefix: "│ ");
        var lines = result.Lines;

        foreach (var line in lines)
        {
            var w = DisplayWidth.GetStringWidth(line);
            Assert.True(w <= 12, $"Line exceeds maxWidth 12: width={w}");
        }
    }

    [Fact]
    public void WrapLinesWithLinks_NoContinuationPrefix_UsesSpaces()
    {
        // Without continuation prefix, continuation lines should use spaces (existing behavior)
        var runs = MarkdownInlineRenderer.FlattenInlines(
            [new TextInline("• one two three four five")]);
        var words = MarkdownInlineRenderer.SplitIntoWords(runs);
        var result = MarkdownInlineRenderer.WrapLinesWithLinks(
            words, 15, hangingIndent: 2, continuationPrefix: null);
        var lines = result.Lines;

        Assert.True(lines.Count >= 2, $"Expected wrapping, got {lines.Count} lines");

        // Continuation lines should start with spaces, not "│ "
        for (int i = 1; i < lines.Count; i++)
        {
            var stripped = StripAnsi(lines[i]);
            Assert.StartsWith("  ", stripped);
            Assert.False(stripped.StartsWith("│"), $"Line {i} should not start with │");
        }
    }

    private static string StripAnsi(string s)
    {
        return System.Text.RegularExpressions.Regex.Replace(s, @"\x1b\[[0-9;]*m", "");
    }

    // ==========================================================================
    // Strikethrough
    // ==========================================================================

    [Fact]
    public void FlattenInlines_Strikethrough_SetsStrikethroughAttribute()
    {
        var inlines = new MarkdownInline[]
        {
            new StrikethroughInline([new TextInline("deleted")])
        };

        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Single(runs);
        Assert.Equal("deleted", runs[0].Text);
        Assert.True(runs[0].Attributes.HasFlag(CellAttributes.Strikethrough));
    }

    [Fact]
    public void FlattenInlines_StrikethroughWithBold_CombinesAttributes()
    {
        var inlines = new MarkdownInline[]
        {
            new StrikethroughInline([
                new EmphasisInline(true, [new TextInline("bold+strike")])
            ])
        };

        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Single(runs);
        Assert.Equal("bold+strike", runs[0].Text);
        Assert.True(runs[0].Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.True(runs[0].Attributes.HasFlag(CellAttributes.Bold));
    }

    [Fact]
    public void FlattenInlines_StrikethroughContainingLink_ExtractsLink()
    {
        var inlines = new MarkdownInline[]
        {
            new StrikethroughInline([
                new LinkInline("click", "https://example.com")
            ])
        };

        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        Assert.Single(runs);
        Assert.Equal("click", runs[0].Text);
        Assert.Equal("https://example.com", runs[0].Url);
        Assert.True(runs[0].Attributes.HasFlag(CellAttributes.Underline));
    }
}
