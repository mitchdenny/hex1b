using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

public class WidgetMethodWithPrefixAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetMethodWithPrefixAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetMethodWithPrefixAnalyzer>.Diagnostic(WidgetMethodWithPrefixAnalyzer.Rule);

    [Fact]
    public async Task ExtensionMethod_WithPrefix_OnConcreteWidget_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget {|#0:WithBar|}(this FooWidget widget, int bar) => widget;
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("WithBar"));
    }

    [Fact]
    public async Task ExtensionMethod_NoPrefix_OnConcreteWidget_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Bar(this FooWidget widget, int bar) => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ExtensionMethod_WithPrefix_OnNonWidgetType_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public static class StringExtensions
            {
                public static string WithPrefix(this string value, string prefix) => prefix + value;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ExtensionMethod_WithPrefix_OnWidgetContext_NoDiagnostic()
    {
        // WidgetContext<T> is the parent-builder context, not a widget itself; the rule
        // intentionally does not flag factory-style extensions on it.
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public static class CtxExtensions
            {
                public static TextBlockWidget WithText<TParent>(this WidgetContext<TParent> ctx, string text)
                    where TParent : Hex1bWidget
                    => new(text);
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ExtensionMethod_WithPrefix_GenericConstrainedToWidget_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public static class WidgetExtensions
            {
                public static TWidget {|#0:WithMetric|}<TWidget>(this TWidget widget, string name)
                    where TWidget : Hex1bWidget
                    => widget;
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("WithMetric"));
    }

    [Fact]
    public async Task ExtensionMethod_WithPrefix_GenericNotConstrainedToWidget_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public static class GenericExtensions
            {
                public static T WithCopy<T>(this T value) => value;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task InstanceMethod_WithPrefix_OnWidgetRecord_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget
            {
                public FooWidget {|#0:WithText|}(string text) => this with { Text = text };
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("WithText"));
    }

    [Fact]
    public async Task InstanceMethod_WithPrefix_OnNonWidgetRecord_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public sealed record Person(string Name)
            {
                public Person WithName(string name) => this with { Name = name };
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ExactlyNamedWith_NoDiagnostic()
    {
        // The rule requires "With" + an uppercase letter so that members literally named
        // "With" or "Within" are not flagged.
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget
            {
                public FooWidget With(string s) => this;
                public FooWidget Within(int n) => this;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task BuilderType_WithPrefix_NoDiagnostic()
    {
        // Builder-style hosts may legitimately use the With* prefix.
        const string source = """
            namespace MyApp;

            public sealed class MyBuilder
            {
                public MyBuilder WithFoo(int n) => this;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task PrivateInstanceMethod_WithPrefix_OnWidget_NoDiagnostic()
    {
        // Internal helpers (private/protected) are implementation details and should not
        // be flagged; only public-or-internal API surface is enforced.
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget
            {
                private FooWidget WithMutated(string text) => this with { Text = text };
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task InstanceMethod_WithPrefix_Internal_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget
            {
                internal FooWidget {|#0:WithFoo|}(string text) => this with { Text = text };
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("WithFoo"));
    }

    [Fact]
    public async Task InstanceMethod_WithPrefix_Override_NoDiagnostic()
    {
        // The violation belongs on the base declaration; subclasses overriding a With* method
        // should not be flagged again because they cannot rename without changing the contract.
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public abstract record BaseFooWidget : Hex1bWidget
            {
                public virtual BaseFooWidget {|#0:WithBar|}(int bar) => this;
            }

            public sealed record FooWidget : BaseFooWidget
            {
                public override BaseFooWidget WithBar(int bar) => this;
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("WithBar"));
    }

    [Fact]
    public async Task InstanceMethod_WithPrefix_ExplicitInterfaceImpl_NoDiagnostic()
    {
        // Explicit interface implementations cannot rename; the violation (if any) belongs on
        // the interface declaration, not the implementer.
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public interface IFooConfigurable
            {
                IFooConfigurable WithBar(int bar);
            }

            public sealed record FooWidget : Hex1bWidget, IFooConfigurable
            {
                IFooConfigurable IFooConfigurable.WithBar(int bar) => this;
            }
            """;

        await VerifyAsync(source);
    }

    [Fact]
    public async Task ExtensionMethod_WithPrefix_NestedGenericConstraintChain_Reports()
    {
        // T : TWidget : Hex1bWidget — analyzer must walk the constraint chain transitively.
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static T {|#0:WithBar|}<T, TWidget>(this T widget, int bar)
                    where TWidget : Hex1bWidget
                    where T : TWidget
                    => widget;
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("WithBar"));
    }
}
