using Hex1b.Documents;
using Hex1b.LanguageServer;
using Hex1b.LanguageServer.Protocol;

namespace Hex1b.Tests.LanguageServer;

[TestClass]
public class LspFeatureControllerTests
{
    private sealed class MinimalExtension : ILanguageExtension
    {
        public string LanguageId { get; init; } = "test";
        public LspFeatureSet EnabledFeatures { get; init; } = LspFeatureSet.All;
    }

    // --- LspFeatureSet flag tests ---

    [TestMethod]
    public void LspFeatureSet_None_HasValueZero()
    {
        Assert.AreEqual(0, (int)LspFeatureSet.None);
    }

    [TestMethod]
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
                Assert.IsTrue((flags[i] & flags[j]) == 0, $"{flags[i]} and {flags[j]} should not overlap");
            }
        }
    }

    [TestMethod]
    [DataRow(LspFeatureSet.SemanticTokens)]
    [DataRow(LspFeatureSet.Completion)]
    [DataRow(LspFeatureSet.Diagnostics)]
    [DataRow(LspFeatureSet.Hover)]
    [DataRow(LspFeatureSet.Definition)]
    [DataRow(LspFeatureSet.References)]
    [DataRow(LspFeatureSet.Rename)]
    [DataRow(LspFeatureSet.SignatureHelp)]
    [DataRow(LspFeatureSet.CodeActions)]
    [DataRow(LspFeatureSet.Formatting)]
    [DataRow(LspFeatureSet.DocumentSymbol)]
    [DataRow(LspFeatureSet.DocumentHighlight)]
    [DataRow(LspFeatureSet.FoldingRange)]
    [DataRow(LspFeatureSet.SelectionRange)]
    [DataRow(LspFeatureSet.InlayHints)]
    [DataRow(LspFeatureSet.CodeLens)]
    [DataRow(LspFeatureSet.CallHierarchy)]
    [DataRow(LspFeatureSet.TypeHierarchy)]
    public void LspFeatureSet_All_IncludesFlag(LspFeatureSet flag)
    {
        Assert.IsTrue((LspFeatureSet.All & flag) != 0, $"All should include {flag}");
    }

    [TestMethod]
    [DataRow(LspFeatureSet.Hover)]
    [DataRow(LspFeatureSet.Completion)]
    [DataRow(LspFeatureSet.Diagnostics)]
    public void LspFeatureSet_Combination_ExcludesRemovedFlag(LspFeatureSet excluded)
    {
        var combined = LspFeatureSet.All & ~excluded;

        Assert.IsTrue((combined & excluded) == 0, $"Combination should not include excluded flag {excluded}");
    }

    [TestMethod]
    public void LspFeatureSet_Combination_RetainsOtherFlags()
    {
        var combined = LspFeatureSet.All & ~LspFeatureSet.Hover;

        Assert.IsTrue((combined & LspFeatureSet.Completion) != 0);
        Assert.IsTrue((combined & LspFeatureSet.Definition) != 0);
        Assert.IsTrue((combined & LspFeatureSet.SemanticTokens) != 0);
    }

    // --- DefaultLanguageExtension tests ---

    [TestMethod]
    public void DefaultExtension_EnablesAllFeatures()
    {
        var extension = new DefaultLanguageExtension("csharp");

        Assert.AreEqual(LspFeatureSet.All, extension.EnabledFeatures);
    }

    [TestMethod]
    public void DefaultExtension_HasCorrectLanguageId()
    {
        var extension = new DefaultLanguageExtension("python");

        Assert.AreEqual("python", extension.LanguageId);
    }

    [TestMethod]
    public void DefaultExtension_Singleton_HasPlaintextLanguageId()
    {
        Assert.AreEqual("plaintext", DefaultLanguageExtension.Instance.LanguageId);
    }

    // --- LspFeatureController.IsEnabled tests ---

    [TestMethod]
    [DataRow(LspFeatureSet.SemanticTokens)]
    [DataRow(LspFeatureSet.Completion)]
    [DataRow(LspFeatureSet.Diagnostics)]
    [DataRow(LspFeatureSet.Hover)]
    [DataRow(LspFeatureSet.Definition)]
    [DataRow(LspFeatureSet.References)]
    [DataRow(LspFeatureSet.Rename)]
    [DataRow(LspFeatureSet.SignatureHelp)]
    [DataRow(LspFeatureSet.CodeActions)]
    [DataRow(LspFeatureSet.Formatting)]
    [DataRow(LspFeatureSet.DocumentSymbol)]
    [DataRow(LspFeatureSet.DocumentHighlight)]
    [DataRow(LspFeatureSet.FoldingRange)]
    [DataRow(LspFeatureSet.SelectionRange)]
    [DataRow(LspFeatureSet.InlayHints)]
    [DataRow(LspFeatureSet.CodeLens)]
    [DataRow(LspFeatureSet.CallHierarchy)]
    [DataRow(LspFeatureSet.TypeHierarchy)]
    public void IsEnabled_AllFeaturesEnabled_ReturnsTrueForEach(LspFeatureSet feature)
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.All };
        var controller = new LspFeatureController(null!, extension);

        Assert.IsTrue(controller.IsEnabled(feature));
    }

    [TestMethod]
    [DataRow(LspFeatureSet.Hover)]
    [DataRow(LspFeatureSet.Completion)]
    [DataRow(LspFeatureSet.Rename)]
    public void IsEnabled_SpecificFeatureDisabled_ReturnsFalse(LspFeatureSet disabled)
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.All & ~disabled };
        var controller = new LspFeatureController(null!, extension);

        Assert.IsFalse(controller.IsEnabled(disabled));
    }

    [TestMethod]
    public void IsEnabled_SpecificFeatureDisabled_OtherFeaturesStillEnabled()
    {
        var extension = new MinimalExtension
        {
            EnabledFeatures = LspFeatureSet.All & ~LspFeatureSet.Hover,
        };
        var controller = new LspFeatureController(null!, extension);

        Assert.IsFalse(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.IsTrue(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.IsTrue(controller.IsEnabled(LspFeatureSet.Definition));
    }

    [TestMethod]
    [DataRow(LspFeatureSet.SemanticTokens)]
    [DataRow(LspFeatureSet.Completion)]
    [DataRow(LspFeatureSet.Hover)]
    [DataRow(LspFeatureSet.CodeActions)]
    public void IsEnabled_None_ReturnsFalseForAll(LspFeatureSet feature)
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.None };
        var controller = new LspFeatureController(null!, extension);

        Assert.IsFalse(controller.IsEnabled(feature));
    }

    [TestMethod]
    public void IsEnabled_OnlyOneFeature_ReturnsTrueOnlyForThat()
    {
        var extension = new MinimalExtension { EnabledFeatures = LspFeatureSet.Hover };
        var controller = new LspFeatureController(null!, extension);

        Assert.IsTrue(controller.IsEnabled(LspFeatureSet.Hover));
        Assert.IsFalse(controller.IsEnabled(LspFeatureSet.Completion));
        Assert.IsFalse(controller.IsEnabled(LspFeatureSet.Definition));
    }

    [TestMethod]
    public void Extension_Property_ReturnsProvidedExtension()
    {
        var extension = new MinimalExtension { LanguageId = "rust" };
        var controller = new LspFeatureController(null!, extension);

        Assert.AreSame(extension, controller.Extension);
    }

    // --- ILanguageExtension default implementation tests ---

    [TestMethod]
    public void ILanguageExtension_DefaultFilterCompletions_ReturnsInput()
    {
        ILanguageExtension extension = new MinimalExtension();
        var items = new List<CompletionItem>
        {
            new() { Label = "foo" },
            new() { Label = "bar" },
        };

        var result = extension.FilterCompletions(items);

        Assert.AreSame(items, result);
    }

    [TestMethod]
    public void ILanguageExtension_DefaultFilterCodeActions_ReturnsInput()
    {
        ILanguageExtension extension = new MinimalExtension();
        var actions = new List<CodeAction>
        {
            new() { Title = "Fix import" },
            new() { Title = "Extract method" },
        };

        var result = extension.FilterCodeActions(actions);

        Assert.AreSame(actions, result);
    }

    [TestMethod]
    public void ILanguageExtension_DefaultRenderHoverOverlay_ReturnsNull()
    {
        ILanguageExtension extension = new MinimalExtension();
        var hover = new HoverResult();
        var position = new DocumentPosition(1, 1);

        var result = extension.RenderHoverOverlay(hover, position);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ILanguageExtension_DefaultGetServerConfiguration_ReturnsNull()
    {
        ILanguageExtension extension = new MinimalExtension();

        var result = extension.GetServerConfiguration();

        Assert.IsNull(result);
    }
}
