using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;

namespace Hex1b.Analyzers.Tests;

[TestClass]
public class WidgetInstanceReceiverNameAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
        => AnalyzerTestHelpers<WidgetInstanceReceiverNameAnalyzer>.VerifyAsync(source, expected);

    private static DiagnosticResult Diagnostic()
        => AnalyzerTestHelpers<WidgetInstanceReceiverNameAnalyzer>.Diagnostic(WidgetInstanceReceiverNameAnalyzer.Rule);

    [TestMethod]
    public async Task Receiver_RoleNamed_OnConcreteWidget_Reports()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record EditorWidget(string Text) : Hex1bWidget;

            public static class EditorExtensions
            {
                public static EditorWidget LanguageServer(this EditorWidget {|#0:editor|}, string url) => editor;
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("editor", "LanguageServer"));
    }

    [TestMethod]
    public async Task Receiver_NamedWidget_OnConcreteWidget_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record EditorWidget(string Text) : Hex1bWidget;

            public static class EditorExtensions
            {
                public static EditorWidget LanguageServer(this EditorWidget widget, string url) => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task Receiver_GenericWidgetConstrained_NamedNonWidget_Reports()
    {
        const string source = """
            using System;
            using Hex1b.Widgets;

            namespace MyApp;

            public static class WidgetExtensions
            {
                public static TWidget InputBindings<TWidget>(this TWidget {|#0:w|}, Action<int> configure)
                    where TWidget : Hex1bWidget => w;
            }
            """;

        await VerifyAsync(source, Diagnostic().WithLocation(0).WithArguments("w", "InputBindings"));
    }

    [TestMethod]
    public async Task Receiver_GenericWidgetConstrained_NamedWidget_NoDiagnostic()
    {
        const string source = """
            using System;
            using Hex1b.Widgets;

            namespace MyApp;

            public static class WidgetExtensions
            {
                public static TWidget InputBindings<TWidget>(this TWidget widget, Action<int> configure)
                    where TWidget : Hex1bWidget => widget;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task WidgetContextReceiver_NoDiagnostic()
    {
        // WidgetContext<T> is HEX1B0006 territory; this analyzer must stay silent.
        const string source = """
            using Hex1b;
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record FooWidget(string Text) : Hex1bWidget;

            public static class FooExtensions
            {
                public static FooWidget Foo<TParent>(this WidgetContext<TParent> ctx, string text)
                    where TParent : Hex1bWidget => new(text);
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task NonWidgetReceiver_NoDiagnostic()
    {
        const string source = """
            namespace MyApp;

            public static class StringExtensions
            {
                public static string AddPrefix(this string editor, string prefix) => prefix + editor;
            }
            """;

        await VerifyAsync(source);
    }

    [TestMethod]
    public async Task NonExtensionMethod_NoDiagnostic()
    {
        const string source = """
            using Hex1b.Widgets;

            namespace MyApp;

            public sealed record EditorWidget(string Text) : Hex1bWidget;

            public static class EditorExtensions
            {
                public static EditorWidget LanguageServer(EditorWidget editor, string url) => editor;
            }
            """;

        await VerifyAsync(source);
    }
}
