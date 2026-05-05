using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

public class WidgetMustBeRecordAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetMustBeRecordAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetMustBeRecordAnalyzer>.Diagnostic(WidgetMustBeRecordAnalyzer.Rule);

    [Fact]
    public async Task RecordWidget_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ClassWidget_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed class {|#0:FooWidget|} : Hex1bWidget;
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("FooWidget"));
    }

    [Fact]
    public async Task NonWidgetClass_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public sealed class Foo;
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task AbstractRecordWidget_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public abstract record FooWidget : Hex1bWidget;
            """;

        await VerifyAsync(source);
    }
}
