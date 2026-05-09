using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

public class WidgetMultipleBuilderCallbacksAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetMultipleBuilderCallbacksAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetMultipleBuilderCallbacksAnalyzer>.Diagnostic(WidgetMultipleBuilderCallbacksAnalyzer.Rule);

    [Fact]
    public async Task TwoBuilders_OnWidgetContextExtension_Reports()
    {
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget Left, Hex1bWidget Right) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget {|#0:HSplitter|}<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> leftBuilder,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> rightBuilder)
                    where TParent : Hex1bWidget
                    => new(leftBuilder(new WidgetContext<FooWidget>()), rightBuilder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("HSplitter", 2));
    }

    [Fact]
    public async Task ThreeBuilders_Reports()
    {
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget A, Hex1bWidget B, Hex1bWidget C) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget {|#0:Tri|}<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> aBuilder,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> bBuilder,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> cBuilder)
                    where TParent : Hex1bWidget
                    => new(
                        aBuilder(new WidgetContext<FooWidget>()),
                        bBuilder(new WidgetContext<FooWidget>()),
                        cBuilder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("Tri", 3));
    }

    [Fact]
    public async Task SingleBuilder_NoDiagnostic()
    {
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget Child) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> builder)
                    where TParent : Hex1bWidget
                    => new(builder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task NoBuilder_NoDiagnostic()
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

    [Fact]
    public async Task SuppressMessage_OnMultiBuilderMethod_NoDiagnostic()
    {
        const string source = """
            using System;
            using System.Diagnostics.CodeAnalysis;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget Left, Hex1bWidget Right) : Hex1bWidget;

            public static class FooExtensions
            {
                [SuppressMessage("Hex1b.ApiDesign", "HEX1B0009", Justification = "Splitter genuinely needs two panes.")]
                public static FooWidget HSplitter<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> leftBuilder,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> rightBuilder)
                    where TParent : Hex1bWidget
                    => new(leftBuilder(new WidgetContext<FooWidget>()), rightBuilder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task TwoBuilders_OnWidgetInstanceExtension_Reports()
    {
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget? Header, Hex1bWidget? Footer) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget {|#0:HeaderAndFooter|}(
                    this FooWidget widget,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> headerBuilder,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> footerBuilder)
                    => widget with
                    {
                        Header = headerBuilder(new WidgetContext<FooWidget>()),
                        Footer = footerBuilder(new WidgetContext<FooWidget>()),
                    };
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("HeaderAndFooter", 2));
    }

    [Fact]
    public async Task TwoActionConfigures_NoDiagnostic()
    {
        // Two Action<T> callbacks aren't builder callbacks; rule must stay silent.
        const string source = """
            using System;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Configure(this FooWidget widget, Action<int> a, Action<int> b) => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task OnXMethod_TwoHandlerCallbacks_NoDiagnostic()
    {
        // 'On*' methods host handler callbacks, not widget-tree builders, so HEX1B0009 must
        // stay silent even when an OnFoo method takes multiple handler callbacks.
        const string source = """
            using System;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget OnDual(
                    this FooWidget widget,
                    Func<int, Hex1bWidget> primary,
                    Func<int, Hex1bWidget> fallback) => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task NonWidgetExtension_NoDiagnostic()
    {
        const string source = """
            using System;

            namespace MyApp;

            public static class StringExtensions
            {
                public static string Combine(this string s, Func<string, string> a, Func<string, string> b)
                    => a(s) + b(s);
            }
            """;

        await VerifyAsync(source);
    }
}
