using System.Text.Json;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

[TestClass]
public class FeatureIntegrationTests
{
    // ── DocumentHighlight → RangeHighlight ───────────────────

    [TestMethod]
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

        TestSeq.Single(result);
        Assert.AreEqual(new DocumentPosition(1, 6), result[0].Start);
        Assert.AreEqual(new DocumentPosition(1, 11), result[0].End);
    }

    [TestMethod]
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

        Assert.AreEqual(4, result.Count);
        Assert.AreEqual(RangeHighlightKind.Default, result[0].Kind);
        Assert.AreEqual(RangeHighlightKind.ReadAccess, result[1].Kind);
        Assert.AreEqual(RangeHighlightKind.WriteAccess, result[2].Kind);
        Assert.AreEqual(RangeHighlightKind.Default, result[3].Kind);
    }

    [TestMethod]
    public void DocumentHighlightsToRangeHighlights_NullReturnsEmpty()
    {
        var result = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(null);
        Assert.IsEmpty(result);
    }

    // ── SignatureHelp → SignaturePanel ────────────────────────

    [TestMethod]
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

        Assert.IsNotNull(panel);
        TestSeq.Single(panel.Signatures);
        Assert.AreEqual("void Foo(int x)", panel.Signatures[0].Label);
        TestSeq.Single(panel.Signatures[0].Parameters);
        Assert.AreEqual("int x", panel.Signatures[0].Parameters[0].Label);
    }

    [TestMethod]
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

        Assert.IsNotNull(panel);
        Assert.AreEqual(0, panel.ActiveSignature);
        Assert.AreEqual(1, panel.ActiveParameter);
    }

    [TestMethod]
    public void SignatureHelpToPanel_NullReturnsNull()
    {
        Assert.IsNull(LanguageServerDecorationProvider.SignatureHelpToPanel(null));
        Assert.IsNull(LanguageServerDecorationProvider.SignatureHelpToPanel(
            new SignatureHelp { Signatures = [] }));
    }

    // ── DocumentSymbol → BreadcrumbData ──────────────────────

    [TestMethod]
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

        Assert.IsNotNull(breadcrumbs);
        TestSeq.Single(breadcrumbs.Symbols);
        Assert.AreEqual("MyClass", breadcrumbs.Symbols[0].Name);
        Assert.AreEqual(BreadcrumbSymbolKind.Class, breadcrumbs.Symbols[0].Kind);
        Assert.AreEqual(new DocumentPosition(1, 1), breadcrumbs.Symbols[0].Start);
        Assert.AreEqual(new DocumentPosition(11, 1), breadcrumbs.Symbols[0].End);
        Assert.IsNotNull(breadcrumbs.Symbols[0].Children);
        TestSeq.Single(breadcrumbs.Symbols[0].Children!);
        Assert.AreEqual("MyMethod", breadcrumbs.Symbols[0].Children![0].Name);
        Assert.AreEqual(BreadcrumbSymbolKind.Method, breadcrumbs.Symbols[0].Children![0].Kind);
    }

    [TestMethod]
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

        Assert.IsNotNull(breadcrumbs);
        Assert.AreEqual(4, breadcrumbs.Symbols.Count);
        Assert.AreEqual(BreadcrumbSymbolKind.Function, breadcrumbs.Symbols[0].Kind);
        Assert.AreEqual(BreadcrumbSymbolKind.Interface, breadcrumbs.Symbols[1].Kind);
        Assert.AreEqual(BreadcrumbSymbolKind.Enum, breadcrumbs.Symbols[2].Kind);
        Assert.AreEqual(BreadcrumbSymbolKind.Struct, breadcrumbs.Symbols[3].Kind);
    }

    [TestMethod]
    public void DocumentSymbolsToBreadcrumbs_NullReturnsNull()
    {
        Assert.IsNull(LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs(null));
        Assert.IsNull(LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs([]));
    }

    // ── FoldingRange → FoldingRegion ─────────────────────────

    [TestMethod]
    public void FoldingRangesToRegions_ConvertsToOneBased()
    {
        var ranges = new[]
        {
            new FoldingRange { StartLine = 0, EndLine = 5, Kind = "region" },
            new FoldingRange { StartLine = 10, EndLine = 20, Kind = null },
        };

        var regions = LanguageServerDecorationProvider.FoldingRangesToRegions(ranges);

        Assert.AreEqual(2, regions.Count);
        Assert.AreEqual(1, regions[0].StartLine);
        Assert.AreEqual(6, regions[0].EndLine);
        Assert.AreEqual(11, regions[1].StartLine);
        Assert.AreEqual(21, regions[1].EndLine);
    }

    [TestMethod]
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

        Assert.AreEqual(4, regions.Count);
        Assert.AreEqual(FoldingRegionKind.Comment, regions[0].Kind);
        Assert.AreEqual(FoldingRegionKind.Imports, regions[1].Kind);
        Assert.AreEqual(FoldingRegionKind.Region, regions[2].Kind);
        Assert.AreEqual(FoldingRegionKind.Region, regions[3].Kind);
    }

    // ── InlayHint → InlineHint ───────────────────────────────

    [TestMethod]
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

        TestSeq.Single(result);
        Assert.AreEqual(new DocumentPosition(5, 11), result[0].Position);
        Assert.AreEqual("string", result[0].Text);
    }

    // ── CodeLens → GutterDecoration ──────────────────────────

    [TestMethod]
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

        TestSeq.Single(result);
        Assert.AreEqual(4, result[0].Line);
        Assert.AreEqual('5', result[0].Character);
        Assert.AreEqual(GutterDecorationKind.Info, result[0].Kind);
    }

    // ── Helpers ──────────────────────────────────────────────

    private static LspRange MakeRange(int startLine, int startChar, int endLine, int endChar) => new()
    {
        Start = new LspPosition { Line = startLine, Character = startChar },
        End = new LspPosition { Line = endLine, Character = endChar },
    };
}
