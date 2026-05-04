using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

public class NodeTypeNameSuffixAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<NodeTypeNameSuffixAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<NodeTypeNameSuffixAnalyzer>.Diagnostic(NodeTypeNameSuffixAnalyzer.Rule);

    [Fact]
    public async Task NodeSuffix_NoDiagnostic()
    {
        const string source = """
            using Hex1b;

            namespace MyApp;

            public sealed class FooNode : Hex1bNode;
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task MissingSuffix_DirectInheritance_Reports()
    {
        const string source = """
            using Hex1b;

            namespace MyApp;

            public sealed class {|#0:Foo|} : Hex1bNode;
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("Foo"));
    }

    [Fact]
    public async Task NonNodeType_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public sealed class Foo;
            """;

        await VerifyAsync(source);
    }
}
