using System.Text.Json;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

public class FeatureIntegrationTests
{
    // ── DocumentHighlight → RangeHighlight ───────────────────

    [Fact]
    public void DocumentHighlightsToRangeHighlights_ConvertsPositions()
    {
        var highlights = new[]
        {
            new DocumentHighlight
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 5 },
                    End = new LspPosition { Line = 0, Character = 10 },
                },
                Kind = 1,
            },
        };

        var result = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(highlights);

        Assert.Single(result);
        Assert.Equal(new DocumentPosition(1, 6), result[0].Start);
        Assert.Equal(new DocumentPosition(1, 11), result[0].End);
    }

    [Fact]
    public void DocumentHighlightsToRangeHighlights_MapsKinds()
    {
        var highlights = new[]
        {
            new DocumentHighlight { Range = MakeRange(0, 0, 0, 1), Kind = 1 },
            new DocumentHighlight { Range = MakeRange(0, 0, 0, 1), Kind = 2 },
            new DocumentHighlight { Range = MakeRange(0, 0, 0, 1), Kind = 3 },
            new DocumentHighlight { Range = MakeRange(0, 0, 0, 1), Kind = null },
        };

        var result = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(highlights);

        Assert.Equal(4, result.Count);
        Assert.Equal(RangeHighlightKind.Default, result[0].Kind);
        Assert.Equal(RangeHighlightKind.ReadAccess, result[1].Kind);
        Assert.Equal(RangeHighlightKind.WriteAccess, result[2].Kind);
        Assert.Equal(RangeHighlightKind.Default, result[3].Kind);
    }

    [Fact]
    public void DocumentHighlightsToRangeHighlights_NullReturnsEmpty()
    {
        var result = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(null);
        Assert.Empty(result);
    }

    // ── SignatureHelp → SignaturePanel ────────────────────────

    [Fact]
    public void SignatureHelpToPanel_ConvertsSingleSignature()
    {
        var help = new SignatureHelp
        {
            Signatures =
            [
                new SignatureInformation
                {
                    Label = "void Foo(int x)",
                    Parameters =
                    [
                        new ParameterInformation
                        {
                            Label = JsonDocument.Parse("\"int x\"").RootElement,
                        },
                    ],
                },
            ],
            ActiveSignature = 0,
            ActiveParameter = 0,
        };

        var panel = LanguageServerDecorationProvider.SignatureHelpToPanel(help);

        Assert.NotNull(panel);
        Assert.Single(panel.Signatures);
        Assert.Equal("void Foo(int x)", panel.Signatures[0].Label);
        Assert.Single(panel.Signatures[0].Parameters);
        Assert.Equal("int x", panel.Signatures[0].Parameters[0].Label);
    }

    [Fact]
    public void SignatureHelpToPanel_PreservesActiveParameter()
    {
        var help = new SignatureHelp
        {
            Signatures =
            [
                new SignatureInformation
                {
                    Label = "void Bar(int a, string b)",
                    Parameters =
                    [
                        new ParameterInformation { Label = JsonDocument.Parse("\"int a\"").RootElement },
                        new ParameterInformation { Label = JsonDocument.Parse("\"string b\"").RootElement },
                    ],
                },
            ],
            ActiveSignature = 0,
            ActiveParameter = 1,
        };

        var panel = LanguageServerDecorationProvider.SignatureHelpToPanel(help);

        Assert.NotNull(panel);
        Assert.Equal(0, panel.ActiveSignature);
        Assert.Equal(1, panel.ActiveParameter);
    }

    [Fact]
    public void SignatureHelpToPanel_NullReturnsNull()
    {
        Assert.Null(LanguageServerDecorationProvider.SignatureHelpToPanel(null));
        Assert.Null(LanguageServerDecorationProvider.SignatureHelpToPanel(
            new SignatureHelp { Signatures = [] }));
    }

    // ── DocumentSymbol → BreadcrumbData ──────────────────────

    [Fact]
    public void DocumentSymbolsToBreadcrumbs_ConvertsHierarchy()
    {
        var symbols = new[]
        {
            new DocumentSymbol
            {
                Name = "MyClass",
                Kind = SymbolKind.Class,
                Range = MakeRange(0, 0, 10, 0),
                SelectionRange = MakeRange(0, 6, 0, 13),
                Children =
                [
                    new DocumentSymbol
                    {
                        Name = "MyMethod",
                        Kind = SymbolKind.Method,
                        Range = MakeRange(2, 4, 5, 4),
                        SelectionRange = MakeRange(2, 9, 2, 17),
                    },
                ],
            },
        };

        var breadcrumbs = LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs(symbols);

        Assert.NotNull(breadcrumbs);
        Assert.Single(breadcrumbs.Symbols);
        Assert.Equal("MyClass", breadcrumbs.Symbols[0].Name);
        Assert.Equal(BreadcrumbSymbolKind.Class, breadcrumbs.Symbols[0].Kind);
        Assert.Equal(new DocumentPosition(1, 1), breadcrumbs.Symbols[0].Start);
        Assert.Equal(new DocumentPosition(11, 1), breadcrumbs.Symbols[0].End);
        Assert.NotNull(breadcrumbs.Symbols[0].Children);
        Assert.Single(breadcrumbs.Symbols[0].Children!);
        Assert.Equal("MyMethod", breadcrumbs.Symbols[0].Children![0].Name);
        Assert.Equal(BreadcrumbSymbolKind.Method, breadcrumbs.Symbols[0].Children![0].Kind);
    }

    [Fact]
    public void DocumentSymbolsToBreadcrumbs_MapsSymbolKinds()
    {
        var symbols = new[]
        {
            new DocumentSymbol { Name = "f", Kind = SymbolKind.Function, Range = MakeRange(0, 0, 0, 1), SelectionRange = MakeRange(0, 0, 0, 1) },
            new DocumentSymbol { Name = "i", Kind = SymbolKind.Interface, Range = MakeRange(0, 0, 0, 1), SelectionRange = MakeRange(0, 0, 0, 1) },
            new DocumentSymbol { Name = "e", Kind = SymbolKind.Enum, Range = MakeRange(0, 0, 0, 1), SelectionRange = MakeRange(0, 0, 0, 1) },
            new DocumentSymbol { Name = "s", Kind = SymbolKind.Struct, Range = MakeRange(0, 0, 0, 1), SelectionRange = MakeRange(0, 0, 0, 1) },
        };

        var breadcrumbs = LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs(symbols);

        Assert.NotNull(breadcrumbs);
        Assert.Equal(4, breadcrumbs.Symbols.Count);
        Assert.Equal(BreadcrumbSymbolKind.Function, breadcrumbs.Symbols[0].Kind);
        Assert.Equal(BreadcrumbSymbolKind.Interface, breadcrumbs.Symbols[1].Kind);
        Assert.Equal(BreadcrumbSymbolKind.Enum, breadcrumbs.Symbols[2].Kind);
        Assert.Equal(BreadcrumbSymbolKind.Struct, breadcrumbs.Symbols[3].Kind);
    }

    [Fact]
    public void DocumentSymbolsToBreadcrumbs_NullReturnsNull()
    {
        Assert.Null(LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs(null));
        Assert.Null(LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs([]));
    }

    // ── FoldingRange → FoldingRegion ─────────────────────────

    [Fact]
    public void FoldingRangesToRegions_ConvertsToOneBased()
    {
        var ranges = new[]
        {
            new FoldingRange { StartLine = 0, EndLine = 5, Kind = "region" },
            new FoldingRange { StartLine = 10, EndLine = 20, Kind = null },
        };

        var regions = LanguageServerDecorationProvider.FoldingRangesToRegions(ranges);

        Assert.Equal(2, regions.Count);
        Assert.Equal(1, regions[0].StartLine);
        Assert.Equal(6, regions[0].EndLine);
        Assert.Equal(11, regions[1].StartLine);
        Assert.Equal(21, regions[1].EndLine);
    }

    [Fact]
    public void FoldingRangesToRegions_MapsKinds()
    {
        var ranges = new[]
        {
            new FoldingRange { StartLine = 0, EndLine = 1, Kind = "comment" },
            new FoldingRange { StartLine = 2, EndLine = 3, Kind = "imports" },
            new FoldingRange { StartLine = 4, EndLine = 5, Kind = "region" },
            new FoldingRange { StartLine = 6, EndLine = 7, Kind = null },
        };

        var regions = LanguageServerDecorationProvider.FoldingRangesToRegions(ranges);

        Assert.Equal(4, regions.Count);
        Assert.Equal(FoldingRegionKind.Comment, regions[0].Kind);
        Assert.Equal(FoldingRegionKind.Imports, regions[1].Kind);
        Assert.Equal(FoldingRegionKind.Region, regions[2].Kind);
        Assert.Equal(FoldingRegionKind.Region, regions[3].Kind);
    }

    // ── InlayHint → InlineHint ───────────────────────────────

    [Fact]
    public void InlayHintsToInlineHints_ConvertsPositions()
    {
        var hints = new[]
        {
            new InlayHint
            {
                Position = new LspPosition { Line = 4, Character = 10 },
                Label = JsonDocument.Parse("\"string\"").RootElement,
            },
        };

        var result = LanguageServerDecorationProvider.InlayHintsToInlineHints(hints);

        Assert.Single(result);
        Assert.Equal(new DocumentPosition(5, 11), result[0].Position);
        Assert.Equal("string", result[0].Text);
    }

    // ── CodeLens → GutterDecoration ──────────────────────────

    [Fact]
    public void CodeLensToGutterDecorations_ConvertsCommandTitle()
    {
        var lenses = new[]
        {
            new CodeLens
            {
                Range = MakeRange(3, 0, 3, 10),
                Command = new Command { Title = "5 references", CommandIdentifier = "show.refs" },
            },
        };

        var result = LanguageServerDecorationProvider.CodeLensToGutterDecorations(lenses);

        Assert.Single(result);
        Assert.Equal(4, result[0].Line);
        Assert.Equal('5', result[0].Character);
        Assert.Equal(GutterDecorationKind.Info, result[0].Kind);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static LspRange MakeRange(int startLine, int startChar, int endLine, int endChar) => new()
    {
        Start = new LspPosition { Line = startLine, Character = startChar },
        End = new LspPosition { Line = endLine, Character = endChar },
    };
}
