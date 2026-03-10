using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

public class LspFeatureControllerTests
{
    private sealed class MinimalExtension : ILanguageExtension
    {
        public string LanguageId { get; init; } = "test";
        public LspFeatureSet EnabledFeatures { get; init; } = LspFeatureSet.All;
    }

    // --- LspFeatureSet flag tests ---

    [Fact]
    public void LspFeatureSet_None_HasValueZero()
    {
        Assert.Equal(0, (int)LspFeatureSet.None);
    }

    [Fact]
    public void LspFeatureSet_IndividualFlags_AreDistinct()
    {
        var flags = new[]
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

        for (var i = 0; i < flags.Length; i++)
        {
            for (var j = i + 1; j < flags.Length; j++)
            {
                Assert.True((flags[i] & flags[j]) == 0,
                    $"{flags[i]} and {flags[j]} should not overlap");
            }
        }
    }

    [Theory]
    [InlineData(LspFeatureSet.SemanticTokens)]
    [InlineData(LspFeatureSet.Completion)]
    [InlineData(LspFeatureSet.Diagnostics)]
    [InlineData(LspFeatureSet.Hover)]
    [InlineData(LspFeatureSet.Definition)]
    [InlineData(LspFeatureSet.References)]
    [InlineData(LspFeatureSet.Rename)]
    [InlineData(LspFeatureSet.SignatureHelp)]
    [InlineData(LspFeatureSet.CodeActions)]
    [InlineData(LspFeatureSet.Formatting)]
    [InlineData(LspFeatureSet.DocumentSymbol)]
    [InlineData(LspFeatureSet.DocumentHighlight)]
    [InlineData(LspFeatureSet.FoldingRange)]
    [InlineData(LspFeatureSet.SelectionRange)]
    [InlineData(LspFeatureSet.InlayHints)]
    [InlineData(LspFeatureSet.CodeLens)]
    [InlineData(LspFeatureSet.CallHierarchy)]
    [InlineData(LspFeatureSet.TypeHierarchy)]
    public void LspFeatureSet_All_IncludesFlag(LspFeatureSet flag)
    {
        Assert.True((LspFeatureSet.All & flag) != 0,
            $"All should include {flag}");
    }

    [Theory]
    [InlineData(LspFeatureSet.Hover)]
    [InlineData(LspFeatureSet.Completion)]
    [InlineData(LspFeatureSet.Diagnostics)]
    public void LspFeatureSet_Combination_ExcludesRemovedFlag(LspFeatureSet excluded)
    {
        var combined = LspFeatureSet.All & ~excluded;

        Assert.True((combined & excluded) == 0,
            $"Combination should not include excluded flag {excluded}");
    }

    [Fact]
    public void LspFeatureSet_Combination_RetainsOtherFlags()
    {
        var combined = LspFeatureSet.All & ~LspFeatureSet.Hover;

        Assert.True((combined & LspFeatureSet.Completion) != 0);
        Assert.True((combined & LspFeatureSet.Definition) != 0);
        Assert.True((combined & LspFeatureSet.SemanticTokens) != 0);
    }

    // --- DefaultLanguageExtension tests ---

    [Fact]
    public void DefaultExtension_EnablesAllFeatures()
    {
        var extension = new DefaultLanguageExtension("csharp");

        Assert.Equal(LspFeatureSet.All, extension.EnabledFeatures);
    }

    [Fact]
    public void DefaultExtension_HasCorrectLanguageId()
    {
        var extension = new DefaultLanguageExtension("python");

        Assert.Equal("python", extension.LanguageId);
    }

    [Fact]
    public void DefaultExtension_Singleton_HasPlaintextLanguageId()
    {
        Assert.Equal("plaintext", DefaultLanguageExtension.Instance.LanguageId);
    }

    // --- LspFeatureController.IsEnabled tests ---

    [Theory]
    [InlineData(LspFeatureSet.SemanticTokens)]
    [InlineData(LspFeatureSet.Completion)]
    [InlineData(LspFeatureSet.Diagnostics)]
    [InlineData(LspFeatureSet.Hover)]
    [InlineData(LspFeatureSet.Definition)]
    [InlineData(LspFeatureSet.References)]
    [InlineData(LspFeatureSet.Rename)]
    [InlineData(LspFeatureSet.SignatureHelp)]
    [InlineData(LspFeatureSet.CodeActions)]
    [InlineData(LspFeatureSet.Formatting)]
    [InlineData(LspFeatureSet.DocumentSymbol)]
    [InlineData(LspFeatureSet.DocumentHighlight)]
    [InlineData(LspFeatureSet.FoldingRange)]
    [InlineData(LspFeatureSet.SelectionRange)]
    [InlineData(LspFeatureSet.InlayHints)]
    [InlineData(LspFeatureSet.CodeLens)]
    [InlineData(LspFeatureSet.CallHierarchy)]
    [InlineData(LspFeatureSet.TypeHierarchy)]
    public void IsEnabled_AllFeaturesEnabled_ReturnsTrueForEach(LspFeatureSet feature)
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.All };
        var controller = new LspFeatureController(null!, extension);

        Assert.True(controller.IsEnabled(feature));
    }

    [Theory]
    [InlineData(LspFeatureSet.Hover)]
    [InlineData(LspFeatureSet.Completion)]
    [InlineData(LspFeatureSet.Rename)]
    public void IsEnabled_SpecificFeatureDisabled_ReturnsFalse(LspFeatureSet disabled)
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.All & ~disabled };
        var controller = new LspFeatureController(null!, extension);

        Assert.False(controller.IsEnabled(disabled));
    }

    [Fact]
    public void IsEnabled_SpecificFeatureDisabled_OtherFeaturesStillEnabled()
    {
        var extension = new MinimalExtension
        {
            EnabledFeatures = LspFeatureSet.All & ~LspFeatureSet.Hover,
        };
        var controller = new LspFeatureController(null!, extension);

        Assert.False(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.True(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.True(controller.IsEnabled(LspFeatureSet.Definition));
    }

    [Theory]
    [InlineData(LspFeatureSet.SemanticTokens)]
    [InlineData(LspFeatureSet.Completion)]
    [InlineData(LspFeatureSet.Hover)]
    [InlineData(LspFeatureSet.CodeActions)]
    public void IsEnabled_None_ReturnsFalseForAll(LspFeatureSet feature)
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.None };
        var controller = new LspFeatureController(null!, extension);

        Assert.False(controller.IsEnabled(feature));
    }

    [Fact]
    public void IsEnabled_OnlyOneFeature_ReturnsTrueOnlyForThat()
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.Hover };
        var controller = new LspFeatureController(null!, extension);

        Assert.True(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.False(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.False(controller.IsEnabled(LspFeatureSet.Definition));
    }

    [Fact]
    public void Extension_Property_ReturnsProvidedExtension()
    {
        var extension = new MinimalExtension { LanguageId = "rust" };
        var controller = new LspFeatureController(null!, extension);

        Assert.Same(extension, controller.Extension);
    }

    // --- ILanguageExtension default implementation tests ---

    [Fact]
    public void ILanguageExtension_DefaultFilterCompletions_ReturnsInput()
    {
        ILanguageExtension extension = new MinimalExtension();
        var items = new List<CompletionItem>
        {
            new() { Label = "foo" },
            new() { Label = "bar" },
        };

        var result = extension.FilterCompletions(items);

        Assert.Same(items, result);
    }

    [Fact]
    public void ILanguageExtension_DefaultFilterCodeActions_ReturnsInput()
    {
        ILanguageExtension extension = new MinimalExtension();
        var actions = new List<CodeAction>
        {
            new() { Title = "Fix import" },
            new() { Title = "Extract method" },
        };

        var result = extension.FilterCodeActions(actions);

        Assert.Same(actions, result);
    }

    [Fact]
    public void ILanguageExtension_DefaultRenderHoverOverlay_ReturnsNull()
    {
        ILanguageExtension extension = new MinimalExtension();
        var hover = new HoverResult();
        var position = new DocumentPosition(1, 1);

        var result = extension.RenderHoverOverlay(hover, position);

        Assert.Null(result);
    }

    [Fact]
    public void ILanguageExtension_DefaultGetServerConfiguration_ReturnsNull()
    {
        ILanguageExtension extension = new MinimalExtension();

        var result = extension.GetServerConfiguration();

        Assert.Null(result);
    }
}
