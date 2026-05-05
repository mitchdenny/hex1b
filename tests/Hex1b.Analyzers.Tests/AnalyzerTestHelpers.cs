using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

/// <summary>
/// Thin wrapper around <see cref="CSharpAnalyzerTest{TAnalyzer, TVerifier}"/> that configures
/// the test compilation with the same reference assemblies and a metadata reference to the
/// live Hex1b assembly so test inputs can resolve real <c>Hex1bWidget</c> / <c>Hex1bNode</c>
/// symbols.
/// </summary>
internal static class AnalyzerTestHelpers<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Runs the analyzer against the supplied source. Expected diagnostics are passed through
    /// as <see cref="DiagnosticResult"/> instances (use the <c>Diagnostic(...)</c> helper below).
    /// </summary>
    public static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new TestImpl
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            // We only care about analyzer diagnostics in these tests; ignore compiler errors
            // (e.g. unimplemented abstract members on stub widgets/nodes) that would otherwise
            // require unrelated boilerplate to satisfy.
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        // Reference the live Hex1b assembly so test inputs that derive from Hex1bWidget / Hex1bNode
        // resolve the same symbols the analyzer is looking for.
        test.TestState.AdditionalReferences.Add(typeof(global::Hex1b.Widgets.Hex1bWidget).Assembly);

        test.ExpectedDiagnostics.AddRange(expected);

        return test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    /// Convenience constructor for a single <see cref="DiagnosticResult"/> targeting the rule
    /// declared by <typeparamref name="TAnalyzer"/>.
    /// </summary>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => new(descriptor);

    private sealed class TestImpl : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        protected override CompilationOptions CreateCompilationOptions()
        {
            // Allow widget records / records-in-general by ensuring a recent enough language
            // version. The default for Net80 reference assemblies is fine, but be explicit.
            var options = (Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions)base.CreateCompilationOptions();
            return options.WithSpecificDiagnosticOptions(
                options.SpecificDiagnosticOptions
                    // Suppress 'use of preview features' noise inside test inputs.
                    .SetItem("CS9057", ReportDiagnostic.Suppress));
        }

        protected override Microsoft.CodeAnalysis.ParseOptions CreateParseOptions()
        {
            var parse = (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions)base.CreateParseOptions();
            return parse.WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest);
        }
    }
}
