using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

[TestClass]
public class WidgetBuilderCallbackNameAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetBuilderCallbackNameAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetBuilderCallbackNameAnalyzer>.Diagnostic(WidgetBuilderCallbackNameAnalyzer.Rule);

    [TestMethod]
    public async Task SingleBuilder_AlternateName_OnWidgetContextExtension_Reports()
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
                    Func<WidgetContext<FooWidget>, Hex1bWidget> {|#0:childBuilder|})
                    where TParent : Hex1bWidget
                    => new(childBuilder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("childBuilder", "Foo"));
    }

    [TestMethod]
    public async Task SingleBuilder_NamedBuilder_NoDiagnostic()
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

    [TestMethod]
    public async Task SingleBuilder_ReturningArray_AlternateName_Reports()
    {
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget[] Children) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, Hex1bWidget[]> {|#0:children|})
                    where TParent : Hex1bWidget
                    => new(children(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("children", "Foo"));
    }

    [TestMethod]
    public async Task SingleBuilder_ReturningIEnumerable_NamedBuilder_NoDiagnostic()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(IEnumerable<Hex1bWidget> Children) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, IEnumerable<Hex1bWidget>> builder)
                    where TParent : Hex1bWidget
                    => new(builder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task SingleBuilder_OnWidgetInstanceExtension_Reports()
    {
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget? Background) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Background(
                    this FooWidget widget,
                    Func<WidgetContext<Hex1bWidget>, Hex1bWidget> {|#0:backgroundBuilder|})
                    => widget with { Background = backgroundBuilder(new WidgetContext<Hex1bWidget>()) };
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("backgroundBuilder", "Background"));
    }

    [TestMethod]
    public async Task TwoBuilders_NoDiagnostic_FromThisRule()
    {
        // HEX1B0009 owns the multi-builder shape; HEX1B0008 must stay silent on it.
        const string source = """
            using System;
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(Hex1bWidget Left, Hex1bWidget Right) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(
                    this WidgetContext<TParent> context,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> leftBuilder,
                    Func<WidgetContext<FooWidget>, Hex1bWidget> rightBuilder)
                    where TParent : Hex1bWidget
                    => new(leftBuilder(new WidgetContext<FooWidget>()), rightBuilder(new WidgetContext<FooWidget>()));
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task ActionConfigure_NotABuilder_NoDiagnostic()
    {
        // Action<T> configure callbacks aren't widget-builder callbacks; the analyzer must ignore them.
        const string source = """
            using System;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Configure(this FooWidget widget, Action<int> configure) => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task OnXMethod_HandlerCallback_NoDiagnostic()
    {
        // 'OnFoo' methods are event-handler decorators per the Hex1b convention; their callback
        // is a 'handler', not a widget-tree builder, even when the return type is a widget shape.
        const string source = """
            using System;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;
            public abstract record FooBlock { }

            public static class FooExtensions
            {
                public static FooWidget OnBlock<TBlock>(
                    this FooWidget widget,
                    Func<int, TBlock, Hex1bWidget> handler)
                    where TBlock : FooBlock => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task NonWidgetExtension_NoDiagnostic()
    {
        const string source = """
            using System;

            namespace MyApp;

            public static class StringExtensions
            {
                public static string Map(this string s, Func<string, string> childBuilder) => childBuilder(s);
            }
            """;

        await VerifyAsync(source);
    }
}
