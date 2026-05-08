using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0007: Receiver of an extension method whose receiver type is (or is constrained to)
/// <c>Hex1b.Widgets.Hex1bWidget</c> must be named <c>widget</c>. Extensions on
/// <c>WidgetContext&lt;T&gt;</c> are owned by HEX1B0006 and are excluded here.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetInstanceReceiverNameAnalyzer : DiagnosticAnalyzer
{
    public const string ExpectedName = "widget";

    private static readonly LocalizableString Title =
        "Widget instance extension receiver must be named 'widget'";

    private static readonly LocalizableString MessageFormat =
        "Receiver parameter '{0}' of widget extension method '{1}' should be named 'widget' for consistency with other widget extension methods";

    private static readonly LocalizableString Description =
        "Hex1b widget instance extensions use the receiver name 'widget' so configuration chains read consistently across the API. Rename role-flavored aliases (e.g. 'editor', 'border', 'panel') to 'widget'.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetInstanceReceiverName,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetInstanceReceiverName));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var widgetType = Hex1bSymbols.GetWidgetType(compilationStart.Compilation);
            if (widgetType is null)
            {
                return;
            }

            // WidgetContext<T> may legitimately be absent in downstream compilations; if it's
            // there we use it to exclude HEX1B0006's territory, otherwise no exclusion needed.
            var widgetContextType = Hex1bSymbols.GetWidgetContextType(compilationStart.Compilation);

            compilationStart.RegisterSymbolAction(
                ctx => AnalyzeMethod(ctx, widgetType, widgetContextType),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        INamedTypeSymbol widgetType,
        INamedTypeSymbol? widgetContextType)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!method.IsExtensionMethod || method.Parameters.IsDefaultOrEmpty)
        {
            return;
        }

        if (method.IsOverride || !method.ExplicitInterfaceImplementations.IsDefaultOrEmpty)
        {
            return;
        }

        var receiver = method.Parameters[0];
        var receiverType = receiver.Type;

        // HEX1B0006 owns WidgetContext<T> receivers.
        if (Hex1bSymbols.IsWidgetContext(receiverType, widgetContextType))
        {
            return;
        }

        if (!Hex1bSymbols.IsWidgetTypeOrConstrainedToWidget(receiverType, widgetType))
        {
            return;
        }

        if (string.Equals(receiver.Name, ExpectedName, System.StringComparison.Ordinal))
        {
            return;
        }

        var location = receiver.Locations.IsDefaultOrEmpty ? Location.None : receiver.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, receiver.Name, method.Name));
    }
}
