using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

public class DefinitionFeatureTests
{
    [Fact]
    public void LocationsToHighlights_WithMatchingUri_ConvertsToHighlights()
    {
        var locations = new[]
        {
            new Location
            {
                Uri = "file:///test.cs",
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 5, Character = 10 },
                    End = new LspPosition { Line = 5, Character = 20 },
                },
            },
        };

        var highlights = LanguageServerDecorationProvider.LocationsToHighlights(locations, "file:///test.cs");

        Assert.Single(highlights);
        Assert.Equal(new DocumentPosition(6, 11), highlights[0].Start); // 0-based -> 1-based
        Assert.Equal(new DocumentPosition(6, 21), highlights[0].End);
        Assert.Equal(RangeHighlightKind.ReadAccess, highlights[0].Kind);
    }

    [Fact]
    public void LocationsToHighlights_WithDifferentUri_FiltersOut()
    {
        var locations = new[]
        {
            new Location
            {
                Uri = "file:///other.cs",
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 0 },
                    End = new LspPosition { Line = 0, Character = 5 },
                },
            },
        };

        var highlights = LanguageServerDecorationProvider.LocationsToHighlights(locations, "file:///test.cs");
        Assert.Empty(highlights);
    }

    [Fact]
    public void LocationsToHighlights_Null_ReturnsEmpty()
    {
        var highlights = LanguageServerDecorationProvider.LocationsToHighlights(null, "file:///test.cs");
        Assert.Empty(highlights);
    }

    [Fact]
    public void LocationsToHighlights_EmptyArray_ReturnsEmpty()
    {
        var highlights = LanguageServerDecorationProvider.LocationsToHighlights([], "file:///test.cs");
        Assert.Empty(highlights);
    }

    [Fact]
    public void LocationsToHighlights_MultipleLocations_ConvertsMatchingOnly()
    {
        var locations = new[]
        {
            new Location
            {
                Uri = "file:///test.cs",
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 1, Character = 0 },
                    End = new LspPosition { Line = 1, Character = 5 },
                },
            },
            new Location
            {
                Uri = "file:///test.cs",
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 10, Character = 3 },
                    End = new LspPosition { Line = 10, Character = 8 },
                },
            },
            new Location
            {
                Uri = "file:///other.cs",
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 0 },
                    End = new LspPosition { Line = 0, Character = 1 },
                },
            },
        };

        var highlights = LanguageServerDecorationProvider.LocationsToHighlights(locations, "file:///test.cs");
        Assert.Equal(2, highlights.Count);
        Assert.Equal(new DocumentPosition(2, 1), highlights[0].Start);
        Assert.Equal(new DocumentPosition(11, 4), highlights[1].Start);
    }
}
