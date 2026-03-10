using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests.LanguageServer;

public class HoverFeatureTests
{
    [Fact]
    public void BuildHoverOverlay_WithPlainText_GeneratesCorrectOverlay()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "int x" },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 3, 5);

        Assert.NotNull(overlay);
        Assert.Equal("lsp-hover", overlay.Id);
        Assert.Single(overlay.Content);
        Assert.Equal(" int x ", overlay.Content[0].Text);
    }

    [Fact]
    public void BuildHoverOverlay_WithMultiLineContent_GeneratesMultipleLines()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent
            {
                Kind = "plaintext",
                Value = "void MyMethod()\nDoes something useful\nReturns nothing",
            },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.NotNull(overlay);
        Assert.Equal(3, overlay.Content.Count);
        Assert.Equal(" void MyMethod() ", overlay.Content[0].Text);
        Assert.Equal(" Does something useful ", overlay.Content[1].Text);
        Assert.Equal(" Returns nothing ", overlay.Content[2].Text);
    }

    [Fact]
    public void BuildHoverOverlay_WithNullResult_ReturnsNull()
    {
        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(null, 1, 1);

        Assert.Null(overlay);
    }

    [Fact]
    public void BuildHoverOverlay_WithEmptyValue_ReturnsNull()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "" },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.Null(overlay);
    }

    [Fact]
    public void BuildHoverOverlay_WithWhitespaceValue_ReturnsNull()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "   " },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.Null(overlay);
    }

    [Fact]
    public void BuildHoverOverlay_HasCorrectAnchorPosition()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "hover info" },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 10, 25);

        Assert.NotNull(overlay);
        Assert.Equal(new DocumentPosition(10, 25), overlay.AnchorPosition);
    }

    [Fact]
    public void BuildHoverOverlay_PlacesAboveAnchor()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "type info" },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.NotNull(overlay);
        Assert.Equal(OverlayPlacement.Above, overlay.Placement);
    }

    [Fact]
    public void BuildHoverOverlay_DismissesOnCursorMove()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "details" },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.NotNull(overlay);
        Assert.True(overlay.DismissOnCursorMove);
    }

    [Fact]
    public void BuildHoverOverlay_LinesHaveExpectedColors()
    {
        var result = new HoverResult
        {
            Contents = new MarkupContent { Kind = "plaintext", Value = "int x" },
        };

        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.NotNull(overlay);
        var line = overlay.Content[0];
        Assert.Equal(Hex1bColor.FromRgb(204, 204, 204), line.Foreground);
        Assert.Equal(Hex1bColor.FromRgb(37, 37, 38), line.Background);
    }

    [Fact]
    public void BuildHoverOverlay_NullContents_ReturnsNull()
    {
        var result = new HoverResult();
        // Contents defaults to new MarkupContent() with Value = ""
        var overlay = LanguageServerDecorationProvider.BuildHoverOverlay(result, 1, 1);

        Assert.Null(overlay);
    }
}
