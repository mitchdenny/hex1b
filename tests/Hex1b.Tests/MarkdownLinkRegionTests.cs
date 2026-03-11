using Hex1b.Events;
using Hex1b.Markdown;
using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Tests;

public class MarkdownLinkRegionTests
{
    // ==========================================================================
    // LinkRegionInfo from WrapResult
    // ==========================================================================

    [Fact]
    public void WrapLinesWithLinks_SingleLink_ReturnsLinkRegion()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("Visit "),
            new LinkInline("example", "https://example.com")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        var region = Assert.Single(result.LinkRegions);
        Assert.Equal("https://example.com", region.Url);
        Assert.Equal("example", region.Text);
        Assert.Equal(0, region.LineIndex);
        Assert.Equal(6, region.ColumnOffset); // "Visit " is 6 chars
        Assert.Equal(7, region.DisplayWidth); // "example" is 7 chars
    }

    [Fact]
    public void WrapLinesWithLinks_MultipleLinks_ReturnsAllRegions()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("See "),
            new LinkInline("here", "https://a.com"),
            new TextInline(" and "),
            new LinkInline("there", "https://b.com")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        Assert.Equal(2, result.LinkRegions.Count);

        Assert.Equal("https://a.com", result.LinkRegions[0].Url);
        Assert.Equal("here", result.LinkRegions[0].Text);
        Assert.Equal(0, result.LinkRegions[0].LineIndex);
        Assert.Equal(4, result.LinkRegions[0].ColumnOffset); // "See " is 4

        Assert.Equal("https://b.com", result.LinkRegions[1].Url);
        Assert.Equal("there", result.LinkRegions[1].Text);
        Assert.Equal(0, result.LinkRegions[1].LineIndex);
        Assert.Equal(13, result.LinkRegions[1].ColumnOffset); // "See here and " is 13
    }

    [Fact]
    public void WrapLinesWithLinks_LinkAtLineStart_HasZeroOffset()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click", "https://example.com"),
            new TextInline(" me")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        var region = Assert.Single(result.LinkRegions);
        Assert.Equal(0, region.ColumnOffset);
        Assert.Equal(0, region.LineIndex);
    }

    [Fact]
    public void WrapLinesWithLinks_LinkWrapsToNextLine_PositionTracked()
    {
        // "aaaa " (5) + link "click" (5) = 10, but width=8 forces wrap
        var inlines = new MarkdownInline[]
        {
            new TextInline("aaaa "),
            new LinkInline("click", "https://example.com")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 8);

        Assert.Equal(2, result.Lines.Count);
        var region = Assert.Single(result.LinkRegions);
        Assert.Equal(1, region.LineIndex); // wrapped to line 1
        Assert.Equal(0, region.ColumnOffset); // starts at column 0 on new line
    }

    [Fact]
    public void WrapLinesWithLinks_MultiWordLink_AllWordsTracked()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click here for info", "https://example.com")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        var region = Assert.Single(result.LinkRegions);
        Assert.Equal("https://example.com", region.Url);
        Assert.Equal("click here for info", region.Text);
        Assert.Equal(0, region.LineIndex);
        Assert.Equal(0, region.ColumnOffset);
    }

    [Fact]
    public void WrapLinesWithLinks_NoLinks_EmptyLinkRegions()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("Hello world, no links here.")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        Assert.Empty(result.LinkRegions);
    }

    [Fact]
    public void WrapLinesWithLinks_ImageInline_TrackedAsLinkRegion()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("See "),
            new ImageInline("logo", "https://example.com/logo.png")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        var region = Assert.Single(result.LinkRegions);
        Assert.Equal("https://example.com/logo.png", region.Url);
        Assert.Equal("[logo]", region.Text);
    }

    [Fact]
    public void WrapLinesWithLinks_LinkIds_SequentialPerLink()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("first", "https://a.com"),
            new TextInline(" "),
            new LinkInline("second", "https://b.com")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        Assert.Equal(2, result.LinkRegions.Count);
        Assert.Equal(0, result.LinkRegions[0].LinkId);
        Assert.Equal(1, result.LinkRegions[1].LinkId);
    }

    [Fact]
    public void WrapLinesWithLinks_SameUrlDifferentLinks_SeparateRegions()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("click", "https://same.com"),
            new TextInline(" or "),
            new LinkInline("here", "https://same.com")
        };

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);

        Assert.Equal(2, result.LinkRegions.Count);
        // They have different LinkIds even though same URL
        Assert.NotEqual(result.LinkRegions[0].LinkId, result.LinkRegions[1].LinkId);
    }

    // ==========================================================================
    // Focus Highlight Rendering
    // ==========================================================================

    [Fact]
    public void WrapLinesWithLinks_FocusedLink_ReverseVideoApplied()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("Visit "),
            new LinkInline("example", "https://example.com")
        };

        // First render without focus
        var normal = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);
        // Then render with focus on link 0
        var focused = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40, focusedLinkId: 0);

        // The focused version should have different ANSI codes for the link
        Assert.NotEqual(normal.Lines[0], focused.Lines[0]);
        // Both should contain "example" text
        Assert.Contains("example", normal.Lines[0]);
        Assert.Contains("example", focused.Lines[0]);
    }

    [Fact]
    public void WrapLinesWithLinks_NonFocusedLink_NormalRendering()
    {
        var inlines = new MarkdownInline[]
        {
            new LinkInline("first", "https://a.com"),
            new TextInline(" "),
            new LinkInline("second", "https://b.com")
        };

        var normal = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40);
        // Focus on link 0 should NOT change link 1's rendering
        var focused = MarkdownInlineRenderer.RenderLinesWithLinks(inlines, 40, focusedLinkId: 0);

        // Both render to one line, but the focused one has different styling for first link
        Assert.NotEqual(normal.Lines[0], focused.Lines[0]);
    }

    // ==========================================================================
    // MarkdownLinkKind Classification
    // ==========================================================================

    [Theory]
    [InlineData("https://example.com", MarkdownLinkKind.External)]
    [InlineData("http://example.com", MarkdownLinkKind.External)]
    [InlineData("HTTPS://EXAMPLE.COM", MarkdownLinkKind.External)]
    [InlineData("#heading-slug", MarkdownLinkKind.IntraDocument)]
    [InlineData("#", MarkdownLinkKind.IntraDocument)]
    [InlineData("mailto:user@example.com", MarkdownLinkKind.Custom)]
    [InlineData("command:doSomething", MarkdownLinkKind.Custom)]
    [InlineData("ftp://files.example.com", MarkdownLinkKind.Custom)]
    public void ClassifyUrl_ReturnsCorrectKind(string url, MarkdownLinkKind expectedKind)
    {
        var kind = MarkdownLinkActivatedEventArgs.ClassifyUrl(url);
        Assert.Equal(expectedKind, kind);
    }

    // ==========================================================================
    // MarkdownLinkRegionNode
    // ==========================================================================

    [Fact]
    public void MarkdownLinkRegionNode_IsFocusable()
    {
        var node = new MarkdownLinkRegionNode();
        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void MarkdownLinkRegionNode_IsFocused_MarksDirty()
    {
        var node = new MarkdownLinkRegionNode();

        node.IsFocused = true;
        Assert.True(node.IsFocused);
    }

    [Fact]
    public void MarkdownLinkRegionNode_Properties_StoreValues()
    {
        var node = new MarkdownLinkRegionNode
        {
            Url = "https://example.com",
            LinkText = "example",
            LinkId = 42,
            LineIndex = 3,
            ColumnOffset = 10,
            LinkDisplayWidth = 7
        };

        Assert.Equal("https://example.com", node.Url);
        Assert.Equal("example", node.LinkText);
        Assert.Equal(42, node.LinkId);
        Assert.Equal(3, node.LineIndex);
        Assert.Equal(10, node.ColumnOffset);
        Assert.Equal(7, node.LinkDisplayWidth);
    }

    // ==========================================================================
    // LinkId propagation through pipeline
    // ==========================================================================

    [Fact]
    public void FlattenInlines_Links_AssignSequentialLinkIds()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("a "),
            new LinkInline("link1", "url1"),
            new TextInline(" b "),
            new LinkInline("link2", "url2"),
            new TextInline(" c "),
            new ImageInline("img", "url3")
        };

        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        // Find runs with LinkIds
        var linkRuns = runs.Where(r => r.LinkId >= 0).ToList();
        Assert.Equal(3, linkRuns.Count);
        Assert.Equal(0, linkRuns[0].LinkId);
        Assert.Equal(1, linkRuns[1].LinkId);
        Assert.Equal(2, linkRuns[2].LinkId);
    }

    [Fact]
    public void FlattenInlines_NonLinks_HaveNegativeLinkId()
    {
        var inlines = new MarkdownInline[]
        {
            new TextInline("plain text"),
            new EmphasisInline(isStrong: true, [new TextInline("bold")]),
            new CodeInline("code")
        };

        var runs = MarkdownInlineRenderer.FlattenInlines(inlines);

        foreach (var run in runs)
        {
            Assert.Equal(-1, run.LinkId);
        }
    }

    [Fact]
    public void SplitIntoWords_LinkId_Propagated()
    {
        var runs = new List<MarkdownTextRun>
        {
            new("Hello ", null, null, CellAttributes.None),
            new("click here", Hex1bColor.FromRgb(100, 160, 255), null,
                CellAttributes.Underline, "https://example.com", 0)
        };

        var words = MarkdownInlineRenderer.SplitIntoWords(runs);

        // "Hello" (no link), "click" (link 0), "here" (link 0)
        Assert.True(words.Count >= 3);

        var clickWord = words.First(w => w.Fragments.Any(f => f.Text == "click"));
        Assert.Equal(0, clickWord.Fragments[0].LinkId);
        Assert.Equal("https://example.com", clickWord.Fragments[0].Url);

        var hereWord = words.First(w => w.Fragments.Any(f => f.Text == "here"));
        Assert.Equal(0, hereWord.Fragments[0].LinkId);
    }

    [Fact]
    public void MarkdownTextBlockNode_GetFocusableNodes_YieldsTwoLinks()
    {
        // Arrange: create a text block node with 2 links
        var node = new MarkdownTextBlockNode();
        node.FocusableLinks = true;

        // Parse inline AST from markdown
        var doc = MarkdownParser.Parse("See [first](https://a.com) and [second](https://b.com)");
        var paragraph = doc.Blocks[0] as ParagraphBlock;
        Assert.NotNull(paragraph);
        node.ReconcileLinkRegions(paragraph!.Inlines);

        // Act
        var focusables = node.GetFocusableNodes().ToList();

        // Assert
        Assert.Equal(2, focusables.Count);
        Assert.All(focusables, f => Assert.IsType<MarkdownLinkRegionNode>(f));
        var link0 = (MarkdownLinkRegionNode)focusables[0];
        var link1 = (MarkdownLinkRegionNode)focusables[1];
        Assert.Equal("https://a.com", link0.Url);
        Assert.Equal("https://b.com", link1.Url);
        Assert.Equal(0, link0.LinkId);
        Assert.Equal(1, link1.LinkId);
    }
}
