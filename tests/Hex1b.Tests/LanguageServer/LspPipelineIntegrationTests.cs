using System.Text.Json;
using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

/// <summary>
/// End-to-end integration tests for the LSP → Extension → Foundation pipeline.
/// Verifies feature gating, extension filtering, type conversions, and feature set
/// combinations WITHOUT a real language server.
/// </summary>
public class LspPipelineIntegrationTests
{
    // ── Helpers ──────────────────────────────────────────────

    private sealed class TestExtension : ILanguageExtension
    {
        public TestExtension(LspFeatureSet features) => EnabledFeatures = features;
        public string LanguageId => "test";
        public LspFeatureSet EnabledFeatures { get; }
    }

    private sealed class FilteringExtension : ILanguageExtension
    {
        public string LanguageId => "test";
        public LspFeatureSet EnabledFeatures => LspFeatureSet.All;

        public IReadOnlyList<CompletionItem> FilterCompletions(IReadOnlyList<CompletionItem> items)
            => items.Where(i => i.Label.StartsWith("test", StringComparison.Ordinal)).ToArray();
    }

    // ── 1. Feature Controller Gating Tests ───────────────────

    [Fact]
    public void Controller_DisabledFeature_ReturnsIsEnabledFalse()
    {
        var extension = new TestExtension(LspFeatureSet.All & ~LspFeatureSet.Hover);
        var controller = new LspFeatureController(null!, extension);

        Assert.False(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.True(controller.IsEnabled(LspFeatureSet.Definition));
        Assert.True(controller.IsEnabled(LspFeatureSet.Completion));
    }

    [Fact]
    public void Controller_NoFeatures_AllDisabled()
    {
        var extension = new TestExtension(LspFeatureSet.None);
        var controller = new LspFeatureController(null!, extension);

        Assert.False(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.False(controller.IsEnabled(LspFeatureSet.Definition));
        Assert.False(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.False(controller.IsEnabled(LspFeatureSet.SemanticTokens));
    }

    [Fact]
    public void Controller_AllFeatures_AllEnabled()
    {
        var extension = new TestExtension(LspFeatureSet.All);
        var controller = new LspFeatureController(null!, extension);

        Assert.True(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.True(controller.IsEnabled(LspFeatureSet.Definition));
        Assert.True(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.True(controller.IsEnabled(LspFeatureSet.SemanticTokens));
        Assert.True(controller.IsEnabled(LspFeatureSet.InlayHints));
        Assert.True(controller.IsEnabled(LspFeatureSet.CodeLens));
    }

    [Fact]
    public void Controller_ExposesExtension()
    {
        var extension = new TestExtension(LspFeatureSet.All);
        var controller = new LspFeatureController(null!, extension);

        Assert.Same(extension, controller.Extension);
    }

    // ── 2. Extension Filter Integration Tests ────────────────

    [Fact]
    public void FilteringExtension_FiltersCompletions()
    {
        var extension = new FilteringExtension();
        var items = new CompletionItem[]
        {
            new() { Label = "testMethod" },
            new() { Label = "otherMethod" },
            new() { Label = "testProperty" },
        };

        var filtered = extension.FilterCompletions(items);

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, i => Assert.StartsWith("test", i.Label));
    }

    [Fact]
    public void FilteringExtension_EmptyInput_ReturnsEmpty()
    {
        var extension = new FilteringExtension();
        var filtered = extension.FilterCompletions([]);

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilteringExtension_NoMatches_ReturnsEmpty()
    {
        var extension = new FilteringExtension();
        var items = new CompletionItem[]
        {
            new() { Label = "otherMethod" },
            new() { Label = "anotherThing" },
        };

        var filtered = extension.FilterCompletions(items);

        Assert.Empty(filtered);
    }

    [Fact]
    public void DefaultExtension_PassthroughCompletions()
    {
        ILanguageExtension extension = DefaultLanguageExtension.Instance;
        var items = new CompletionItem[]
        {
            new() { Label = "alpha" },
            new() { Label = "beta" },
        };

        var filtered = extension.FilterCompletions(items);

        Assert.Same(items, filtered);
    }

    [Fact]
    public void DefaultExtension_AllFeaturesEnabled()
    {
        var extension = DefaultLanguageExtension.Instance;

        Assert.Equal(LspFeatureSet.All, extension.EnabledFeatures);
    }

    // ── 3. Full Pipeline Tests (LSP types → Foundation types) ─

    [Fact]
    public void Pipeline_DocumentHighlights_ToRangeHighlights_EndToEnd()
    {
        var lspHighlights = new DocumentHighlight[]
        {
            new()
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 5, Character = 10 },
                    End = new LspPosition { Line = 5, Character = 15 },
                },
                Kind = 2, // Read
            },
            new()
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 10, Character = 0 },
                    End = new LspPosition { Line = 10, Character = 5 },
                },
                Kind = 3, // Write
            },
        };

        var rangeHighlights = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(lspHighlights);

        Assert.Equal(2, rangeHighlights.Count);
        // 0-based → 1-based
        Assert.Equal(new DocumentPosition(6, 11), rangeHighlights[0].Start);
        Assert.Equal(new DocumentPosition(6, 16), rangeHighlights[0].End);
        Assert.Equal(RangeHighlightKind.ReadAccess, rangeHighlights[0].Kind);
        Assert.Equal(new DocumentPosition(11, 1), rangeHighlights[1].Start);
        Assert.Equal(new DocumentPosition(11, 6), rangeHighlights[1].End);
        Assert.Equal(RangeHighlightKind.WriteAccess, rangeHighlights[1].Kind);
    }

    [Fact]
    public void Pipeline_DocumentHighlights_DefaultKind()
    {
        var lspHighlights = new DocumentHighlight[]
        {
            new()
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 0 },
                    End = new LspPosition { Line = 0, Character = 5 },
                },
                Kind = 1, // Text → Default
            },
        };

        var rangeHighlights = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(lspHighlights);

        Assert.Single(rangeHighlights);
        Assert.Equal(RangeHighlightKind.Default, rangeHighlights[0].Kind);
    }

    [Fact]
    public void Pipeline_DocumentHighlights_NullInput_ReturnsEmpty()
    {
        var result = LanguageServerDecorationProvider.DocumentHighlightsToRangeHighlights(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Pipeline_FoldingRanges_ToFoldingRegions_EndToEnd()
    {
        var lspRanges = new FoldingRange[]
        {
            new() { StartLine = 0, EndLine = 10, Kind = "comment" },
            new() { StartLine = 15, EndLine = 20, Kind = "imports" },
            new() { StartLine = 25, EndLine = 50 }, // no kind
        };

        var regions = LanguageServerDecorationProvider.FoldingRangesToRegions(lspRanges);

        Assert.Equal(3, regions.Count);
        Assert.Equal(1, regions[0].StartLine); // 0-based → 1-based
        Assert.Equal(11, regions[0].EndLine);
        Assert.Equal(FoldingRegionKind.Comment, regions[0].Kind);
        Assert.Equal(16, regions[1].StartLine);
        Assert.Equal(21, regions[1].EndLine);
        Assert.Equal(FoldingRegionKind.Imports, regions[1].Kind);
        Assert.Equal(26, regions[2].StartLine);
        Assert.Equal(51, regions[2].EndLine);
        Assert.Equal(FoldingRegionKind.Region, regions[2].Kind); // default
    }

    [Fact]
    public void Pipeline_FoldingRanges_NullInput_ReturnsEmpty()
    {
        var result = LanguageServerDecorationProvider.FoldingRangesToRegions(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Pipeline_InlayHints_ToInlineHints_EndToEnd()
    {
        var lspHints = new InlayHint[]
        {
            new()
            {
                Position = new LspPosition { Line = 3, Character = 15 },
                Label = JsonSerializer.SerializeToElement("string"),
            },
            new()
            {
                Position = new LspPosition { Line = 7, Character = 0 },
                Label = JsonSerializer.SerializeToElement("int"),
            },
        };

        var inlineHints = LanguageServerDecorationProvider.InlayHintsToInlineHints(lspHints);

        Assert.Equal(2, inlineHints.Count);
        Assert.Equal(new DocumentPosition(4, 16), inlineHints[0].Position); // 0→1 based
        Assert.Equal("string", inlineHints[0].Text);
        Assert.Equal(new DocumentPosition(8, 1), inlineHints[1].Position);
        Assert.Equal("int", inlineHints[1].Text);
    }

    [Fact]
    public void Pipeline_InlayHints_ArrayLabel_ConcatenatesParts()
    {
        var labelParts = new[]
        {
            new { value = "param" },
            new { value = ": " },
            new { value = "int" },
        };
        var lspHints = new InlayHint[]
        {
            new()
            {
                Position = new LspPosition { Line = 0, Character = 5 },
                Label = JsonSerializer.SerializeToElement(labelParts),
            },
        };

        var inlineHints = LanguageServerDecorationProvider.InlayHintsToInlineHints(lspHints);

        Assert.Single(inlineHints);
        Assert.Equal("param: int", inlineHints[0].Text);
    }

    [Fact]
    public void Pipeline_InlayHints_NullInput_ReturnsEmpty()
    {
        var result = LanguageServerDecorationProvider.InlayHintsToInlineHints(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Pipeline_DocumentSymbols_ToBreadcrumbs_EndToEnd()
    {
        var lspSymbols = new DocumentSymbol[]
        {
            new()
            {
                Name = "MyClass",
                Kind = SymbolKind.Class,
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 0 },
                    End = new LspPosition { Line = 50, Character = 0 },
                },
                SelectionRange = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 6 },
                    End = new LspPosition { Line = 0, Character = 13 },
                },
                Children = new DocumentSymbol[]
                {
                    new()
                    {
                        Name = "DoWork",
                        Kind = SymbolKind.Method,
                        Range = new LspRange
                        {
                            Start = new LspPosition { Line = 5, Character = 4 },
                            End = new LspPosition { Line = 20, Character = 4 },
                        },
                        SelectionRange = new LspRange
                        {
                            Start = new LspPosition { Line = 5, Character = 16 },
                            End = new LspPosition { Line = 5, Character = 22 },
                        },
                    },
                },
            },
        };

        var breadcrumbs = LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs(lspSymbols);

        Assert.NotNull(breadcrumbs);
        Assert.Single(breadcrumbs!.Symbols);
        Assert.Equal("MyClass", breadcrumbs.Symbols[0].Name);
        Assert.Equal(BreadcrumbSymbolKind.Class, breadcrumbs.Symbols[0].Kind);
        Assert.Equal(new DocumentPosition(1, 1), breadcrumbs.Symbols[0].Start);
        Assert.Equal(new DocumentPosition(51, 1), breadcrumbs.Symbols[0].End);
        Assert.NotNull(breadcrumbs.Symbols[0].Children);
        Assert.Single(breadcrumbs.Symbols[0].Children!);
        Assert.Equal("DoWork", breadcrumbs.Symbols[0].Children![0].Name);
        Assert.Equal(BreadcrumbSymbolKind.Method, breadcrumbs.Symbols[0].Children![0].Kind);
    }

    [Fact]
    public void Pipeline_DocumentSymbols_NullInput_ReturnsNull()
    {
        var result = LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs(null);

        Assert.Null(result);
    }

    [Fact]
    public void Pipeline_DocumentSymbols_EmptyInput_ReturnsNull()
    {
        var result = LanguageServerDecorationProvider.DocumentSymbolsToBreadcrumbs([]);

        Assert.Null(result);
    }

    [Fact]
    public void Pipeline_SignatureHelp_ToSignaturePanel_EndToEnd()
    {
        var lspHelp = new SignatureHelp
        {
            Signatures = new SignatureInformation[]
            {
                new()
                {
                    Label = "void Foo(int x, string y)",
                    Parameters = new ParameterInformation[]
                    {
                        new() { Label = JsonSerializer.SerializeToElement("int x") },
                        new() { Label = JsonSerializer.SerializeToElement("string y") },
                    },
                },
            },
            ActiveSignature = 0,
            ActiveParameter = 1,
        };

        var panel = LanguageServerDecorationProvider.SignatureHelpToPanel(lspHelp);

        Assert.NotNull(panel);
        Assert.Single(panel!.Signatures);
        Assert.Equal("void Foo(int x, string y)", panel.Signatures[0].Label);
        Assert.Equal(2, panel.Signatures[0].Parameters.Count);
        Assert.Equal("int x", panel.Signatures[0].Parameters[0].Label);
        Assert.Equal("string y", panel.Signatures[0].Parameters[1].Label);
        Assert.Equal(0, panel.ActiveSignature);
        Assert.Equal(1, panel.ActiveParameter);
    }

    [Fact]
    public void Pipeline_SignatureHelp_NullInput_ReturnsNull()
    {
        var result = LanguageServerDecorationProvider.SignatureHelpToPanel(null);

        Assert.Null(result);
    }

    [Fact]
    public void Pipeline_SignatureHelp_EmptySignatures_ReturnsNull()
    {
        var result = LanguageServerDecorationProvider.SignatureHelpToPanel(
            new SignatureHelp { Signatures = [] });

        Assert.Null(result);
    }

    [Fact]
    public void Pipeline_SignatureHelp_NullActiveIndices_DefaultsToZero()
    {
        var lspHelp = new SignatureHelp
        {
            Signatures = new SignatureInformation[]
            {
                new()
                {
                    Label = "void Bar()",
                    Parameters = [],
                },
            },
            ActiveSignature = null,
            ActiveParameter = null,
        };

        var panel = LanguageServerDecorationProvider.SignatureHelpToPanel(lspHelp);

        Assert.NotNull(panel);
        Assert.Equal(0, panel!.ActiveSignature);
        Assert.Equal(0, panel.ActiveParameter);
    }

    [Fact]
    public void Pipeline_CodeLens_ToGutterDecorations_EndToEnd()
    {
        var lspLenses = new CodeLens[]
        {
            new()
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 4, Character = 0 },
                    End = new LspPosition { Line = 4, Character = 10 },
                },
                Command = new Command { Title = "3 references", CommandIdentifier = "showReferences" },
            },
            new()
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 12, Character = 0 },
                    End = new LspPosition { Line = 12, Character = 5 },
                },
                Command = new Command { Title = "Run test", CommandIdentifier = "runTest" },
            },
        };

        var decorations = LanguageServerDecorationProvider.CodeLensToGutterDecorations(lspLenses);

        Assert.Equal(2, decorations.Count);
        Assert.Equal(5, decorations[0].Line); // 0-based → 1-based
        Assert.Equal('3', decorations[0].Character); // first char of title
        Assert.Equal(GutterDecorationKind.Info, decorations[0].Kind);
        Assert.Equal(13, decorations[1].Line);
        Assert.Equal('R', decorations[1].Character);
    }

    [Fact]
    public void Pipeline_CodeLens_NullCommand_Skipped()
    {
        var lspLenses = new CodeLens[]
        {
            new()
            {
                Range = new LspRange
                {
                    Start = new LspPosition { Line = 0, Character = 0 },
                    End = new LspPosition { Line = 0, Character = 5 },
                },
                Command = null,
            },
        };

        var decorations = LanguageServerDecorationProvider.CodeLensToGutterDecorations(lspLenses);

        Assert.Empty(decorations);
    }

    [Fact]
    public void Pipeline_CodeLens_NullInput_ReturnsEmpty()
    {
        var result = LanguageServerDecorationProvider.CodeLensToGutterDecorations(null);

        Assert.Empty(result);
    }

    // ── 4. Feature Set Combination Tests ─────────────────────

    [Fact]
    public void FeatureSet_SelectiveDisable_WorksEndToEnd()
    {
        var features = LspFeatureSet.All & ~LspFeatureSet.Hover & ~LspFeatureSet.InlayHints;
        var extension = new TestExtension(features);
        var controller = new LspFeatureController(null!, extension);

        Assert.False(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.False(controller.IsEnabled(LspFeatureSet.InlayHints));
        Assert.True(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.True(controller.IsEnabled(LspFeatureSet.Definition));
        Assert.True(controller.IsEnabled(LspFeatureSet.CodeActions));
        Assert.True(controller.IsEnabled(LspFeatureSet.FoldingRange));
    }

    [Fact]
    public void FeatureSet_SingleFeatureEnabled()
    {
        var extension = new TestExtension(LspFeatureSet.Completion);
        var controller = new LspFeatureController(null!, extension);

        Assert.True(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.False(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.False(controller.IsEnabled(LspFeatureSet.Definition));
        Assert.False(controller.IsEnabled(LspFeatureSet.SemanticTokens));
    }

    [Fact]
    public void FeatureSet_MultipleExplicitFeatures()
    {
        var features = LspFeatureSet.Hover | LspFeatureSet.Completion | LspFeatureSet.Definition;
        var extension = new TestExtension(features);
        var controller = new LspFeatureController(null!, extension);

        Assert.True(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.True(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.True(controller.IsEnabled(LspFeatureSet.Definition));
        Assert.False(controller.IsEnabled(LspFeatureSet.References));
        Assert.False(controller.IsEnabled(LspFeatureSet.InlayHints));
        Assert.False(controller.IsEnabled(LspFeatureSet.CodeLens));
    }

    [Fact]
    public void FeatureSet_None_IsZero()
    {
        Assert.Equal((LspFeatureSet)0, LspFeatureSet.None);
    }

    [Fact]
    public void FeatureSet_All_ContainsEveryFeature()
    {
        var allFeatures = new[]
        {
            LspFeatureSet.SemanticTokens,
            LspFeatureSet.Completion,
            LspFeatureSet.Diagnostics,
            LspFeatureSet.Hover,
            LspFeatureSet.Definition,
            LspFeatureSet.References,
            LspFeatureSet.Rename,
            LspFeatureSet.SignatureHelp,
            LspFeatureSet.CodeActions,
            LspFeatureSet.Formatting,
            LspFeatureSet.DocumentSymbol,
            LspFeatureSet.DocumentHighlight,
            LspFeatureSet.FoldingRange,
            LspFeatureSet.SelectionRange,
            LspFeatureSet.InlayHints,
            LspFeatureSet.CodeLens,
            LspFeatureSet.CallHierarchy,
            LspFeatureSet.TypeHierarchy,
        };

        foreach (var feature in allFeatures)
        {
            Assert.True(
                (LspFeatureSet.All & feature) != 0,
                $"LspFeatureSet.All should contain {feature}");
        }
    }
}
