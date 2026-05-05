using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

public class WidgetTypeNameSuffixAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetTypeNameSuffixAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetTypeNameSuffixAnalyzer>.Diagnostic(WidgetTypeNameSuffixAnalyzer.Rule);

    [Fact]
    public async Task WidgetSuffix_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task MissingSuffix_DirectInheritance_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record {|#0:Foo|}(string Text) : Hex1bWidget;
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("Foo"));
    }

    [Fact]
    public async Task MissingSuffix_TransitiveInheritance_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public record IntermediateWidget(string Text) : Hex1bWidget;
            public record {|#0:CustomThing|}(string Text) : IntermediateWidget(Text);
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("CustomThing"));
    }

    [Fact]
    public async Task NonWidgetType_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public sealed record Foo(string Text);
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task GenericWidgetSuffix_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record TypedWidget<T>(T Value) : Hex1bWidget;
            """;

        await VerifyAsync(source);
    }
}
