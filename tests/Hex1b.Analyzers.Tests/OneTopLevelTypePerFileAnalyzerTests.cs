using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers.Tests;

[TestClass]
public class OneTopLevelTypePerFileAnalyzerTests
{
    [TestMethod]
    public async Task Analyze_NotEnabled_NoDiagnostics()
    {
        var diagnostics = await AnalyzeAsync(["class First { } class Second { }"], enabled: false);

        Assert.IsFalse(OneTopLevelTypePerFileAnalyzer.Rule.IsEnabledByDefault);
        Assert.AreEqual(DiagnosticSeverity.Error, OneTopLevelTypePerFileAnalyzer.Rule.DefaultSeverity);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("namespace Example { }")]
    [DataRow("class Only { }")]
    [DataRow("namespace Example; public record Only(int Value);")]
    [DataRow("namespace Example { namespace Inner { interface Only { } } }")]
    [DataRow("System.Console.WriteLine(\"No explicit type\");")]
    public async Task Analyze_AtMostOneType_NoDiagnostics(string source)
    {
        Assert.IsEmpty(await AnalyzeAsync([source]));
    }

    [TestMethod]
    [DataRow("class Second { }")]
    [DataRow("struct Second { }")]
    [DataRow("record Second;")]
    [DataRow("record struct Second;")]
    [DataRow("interface Second { }")]
    [DataRow("enum Second { Value }")]
    [DataRow("delegate void Second();")]
    [DataRow("file class Second { }")]
    public async Task Analyze_AdditionalTopLevelType_ReportsErrorOnIdentifier(string declaration)
    {
        var source = $"namespace Example;\nclass First {{ }}\n{declaration}";

        var diagnostic = TestSeq.Single(await AnalyzeAsync([source]));

        Assert.AreEqual("HEX1B0011", diagnostic.Id);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.AreEqual("Move top-level type 'Second' to its own file", diagnostic.GetMessage());
        Assert.AreEqual("Second", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [TestMethod]
    public async Task Analyze_ThreeTypes_ReportsEachAdditionalType()
    {
        var diagnostics = await AnalyzeAsync(["class First { } struct Second { } delegate void Third();"]);

        Assert.HasCount(2, diagnostics);
        TestSeq.AreEqual(
            new[] { "Move top-level type 'Second' to its own file", "Move top-level type 'Third' to its own file" },
            diagnostics.Select(d => d.GetMessage()));
    }

    [TestMethod]
    public async Task Analyze_NestedTypes_NoDiagnostics()
    {
        const string source = """
            class Outer
            {
                class Inner { class Deeper { } }
                struct Value { }
                record Model;
                record struct Point;
                interface Contract { }
                enum Choice { One }
                delegate void Callback();
            }
            """;

        Assert.IsEmpty(await AnalyzeAsync([source]));
    }

    [TestMethod]
    public async Task Analyze_NestedTypesAndSecondTopLevelType_ReportsOnlyTopLevelType()
    {
        var diagnostic = TestSeq.Single(await AnalyzeAsync(
            ["class Outer { class Inner { } } class Second { class Nested { } }"]));

        Assert.AreEqual("Move top-level type 'Second' to its own file", diagnostic.GetMessage());
    }

    [TestMethod]
    [DataRow("namespace A { class First { } } namespace B { class Second { } }")]
    [DataRow("class First { } namespace A { namespace B { class Second { } } }")]
    [DataRow("namespace A { class First { } namespace B { class Second { } } }")]
    public async Task Analyze_TypesAcrossNamespaces_ReportsAdditionalType(string source)
    {
        var diagnostic = TestSeq.Single(await AnalyzeAsync([source]));

        Assert.AreEqual("Move top-level type 'Second' to its own file", diagnostic.GetMessage());
    }

    [TestMethod]
    public async Task Analyze_SameNameInDifferentNamespaces_ReportsDistinctType()
    {
        var diagnostic = TestSeq.Single(await AnalyzeAsync(
            ["namespace A { class Item { } } namespace B { class Item { } }"]));

        Assert.AreEqual("Move top-level type 'Item' to its own file", diagnostic.GetMessage());
    }

    [TestMethod]
    public async Task Analyze_SameNameWithDifferentArity_ReportsDistinctType()
    {
        var diagnostic = TestSeq.Single(await AnalyzeAsync(["class Item { } class Item<T> { }"]));

        Assert.AreEqual("Move top-level type 'Item' to its own file", diagnostic.GetMessage());
    }

    [TestMethod]
    [DataRow("partial class Item { } partial class Item { }")]
    [DataRow("partial record Item; partial record Item;")]
    [DataRow("namespace A { partial class Item { } } namespace A { partial class Item { } }")]
    public async Task Analyze_PartialDeclarationsOfSameType_NoDiagnostics(string source)
    {
        Assert.IsEmpty(await AnalyzeAsync([source]));
    }

    [TestMethod]
    public async Task Analyze_PartialDeclarationsWithDistinctType_ReportsDistinctTypeOnce()
    {
        var diagnostics = await AnalyzeAsync(
            ["partial class First { } partial class Second { } partial class First { } partial class Second { }"]);

        Assert.AreEqual("Move top-level type 'Second' to its own file", TestSeq.Single(diagnostics).GetMessage());
    }

    [TestMethod]
    public async Task Analyze_OneTypePerFile_NoDiagnostics()
    {
        Assert.IsEmpty(await AnalyzeAsync(["class First { }", "class Second { }"]));
    }

    [TestMethod]
    public async Task Analyze_PartialTypeAcrossFiles_NoDiagnostics()
    {
        Assert.IsEmpty(await AnalyzeAsync(["partial class Item { }", "partial class Item { }"]));
    }

    [TestMethod]
    public async Task Analyze_SecondFileHasMultipleTypes_ReportsThatFile()
    {
        var diagnostic = TestSeq.Single(await AnalyzeAsync(["class First { }", "class Second { } class Third { }"]));

        Assert.AreEqual("Test1.cs", diagnostic.Location.SourceTree!.FilePath);
        Assert.AreEqual("Move top-level type 'Third' to its own file", diagnostic.GetMessage());
    }

    [TestMethod]
    [DataRow("Types.g.cs")]
    [DataRow("Types.generated.cs")]
    [DataRow("Types.designer.cs")]
    public async Task Analyze_GeneratedFilename_NoDiagnostics(string path)
    {
        Assert.IsEmpty(await AnalyzeAsync(["class First { } class Second { }"], path: path));
    }

    [TestMethod]
    public async Task Analyze_GeneratedHeader_NoDiagnostics()
    {
        Assert.IsEmpty(await AnalyzeAsync(["// <auto-generated/>\nclass First { } class Second { }"]));
    }

    [TestMethod]
    public async Task Analyze_InactivePreprocessorBranch_NoDiagnostics()
    {
        const string source = """
            #if UNDEFINED
            class Inactive { }
            #endif
            class Active { }
            """;

        Assert.IsEmpty(await AnalyzeAsync([source]));
    }

    [TestMethod]
    public async Task Analyze_PragmaSuppression_NoDiagnostics()
    {
        Assert.IsEmpty(await AnalyzeAsync(
            ["#pragma warning disable HEX1B0011\nclass First { } class Second { }"]));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string[] sources, bool enabled = true, string? path = null)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        if (enabled)
        {
            options = options.WithSpecificDiagnosticOptions(
                options.SpecificDiagnosticOptions.SetItem("HEX1B0011", ReportDiagnostic.Error));
        }

        var compilation = CSharpCompilation.Create(
            "AnalyzerInput",
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(
                source, new CSharpParseOptions(LanguageVersion.Preview), path ?? $"Test{index}.cs")),
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options);

        return await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new OneTopLevelTypePerFileAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }
}
