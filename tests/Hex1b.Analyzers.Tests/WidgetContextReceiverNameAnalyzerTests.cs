using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

[TestClass]
public class WidgetContextReceiverNameAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetContextReceiverNameAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetContextReceiverNameAnalyzer>.Diagnostic(WidgetContextReceiverNameAnalyzer.Rule);

    [TestMethod]
    public async Task Receiver_NamedCtx_Reports()
    {
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(this WidgetContext<TParent> {|#0:ctx|}, string text)
                    where TParent : Hex1bWidget => new(text);
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("ctx", "Foo"));
    }

    [TestMethod]
    public async Task Receiver_NamedContext_NoDiagnostic()
    {
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(this WidgetContext<TParent> context, string text)
                    where TParent : Hex1bWidget => new(text);
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task Receiver_AlternateName_Reports()
    {
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(this WidgetContext<TParent> {|#0:builder|}, string text)
                    where TParent : Hex1bWidget => new(text);
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("builder", "Foo"));
    }

    [TestMethod]
    public async Task NonExtensionMethod_NoDiagnostic()
    {
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(WidgetContext<TParent> ctx, string text)
                    where TParent : Hex1bWidget => new(text);
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task NonWidgetContextReceiver_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public static class StringExtensions
            {
                public static string AddPrefix(this string ctx, string prefix) => prefix + ctx;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task WidgetReceiver_NoDiagnostic()
    {
        // Receiver is a widget instance, not WidgetContext<T> — HEX1B0007 territory.
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Title(this FooWidget ctx, string text) => ctx with { Text = text };
            }
            """;

        await VerifyAsync(source);
    }
}
