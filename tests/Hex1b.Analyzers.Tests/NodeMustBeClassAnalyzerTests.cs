using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

public class NodeMustBeClassAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<NodeMustBeClassAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<NodeMustBeClassAnalyzer>.Diagnostic(NodeMustBeClassAnalyzer.Rule);

    [Fact]
    public async Task ClassNode_NoDiagnostic()
    {
        const string source = """
            using Hex1b;

            namespace MyApp;

            public sealed class FooNode : Hex1bNode;
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task RecordNode_Reports()
    {
        const string source = """
            using Hex1b;

            namespace MyApp;

            public sealed record {|#0:FooNode|} : Hex1bNode;
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("FooNode"));
    }

    [Fact]
    public async Task AbstractClassNode_NoDiagnostic()
    {
        const string source = """
            using Hex1b;

            namespace MyApp;

            public abstract class CustomNode : Hex1bNode;
            """;

        await VerifyAsync(source);
    }
}
