using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

[TestClass]
public class WidgetOverridesBuildAndReconcileAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetOverridesBuildAndReconcileAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetOverridesBuildAndReconcileAnalyzer>.Diagnostic(WidgetOverridesBuildAndReconcileAnalyzer.Rule);

    [TestMethod]
    public async Task NoOverrides_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task OverridesBuildOnly_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;
            using Hex1b.Composition;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget
            {
                protected override Hex1bWidget? Build(CompositionContext ctx) => null;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task OverridesReconcileOnly_NoDiagnostic()
    {
        // Note: ReconcileAsync is internal, so this scenario is only realistic for code
        // inside Hex1b itself. We exercise it here by setting the test compilation's
        // assembly name to one that has InternalsVisibleTo (see AnalyzerTestHelpers).
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget
            {
                internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
                    => throw new NotImplementedException();

                internal override Type GetExpectedNodeType() => throw new NotImplementedException();
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task OverridesBoth_Reports()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using Hex1b;
            using Hex1b.Widgets;
            using Hex1b.Composition;

            namespace MyApp;

            public sealed record {|#0:FooWidget|}(string Text) : Hex1bWidget
            {
                protected override Hex1bWidget? Build(CompositionContext ctx) => null;

                internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
                    => throw new NotImplementedException();

                internal override Type GetExpectedNodeType() => throw new NotImplementedException();
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("FooWidget"));
    }

    [TestMethod]
    public async Task NonWidgetClass_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public sealed class Foo
            {
                public string Build() => string.Empty;
                public void ReconcileAsync() { }
            }
            """;

        await VerifyAsync(source);
    }
}
