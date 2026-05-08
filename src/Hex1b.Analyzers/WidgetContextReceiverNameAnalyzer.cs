using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0006: Receiver of an extension method on <c>Hex1b.WidgetContext&lt;T&gt;</c> must
/// be named <c>context</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetContextReceiverNameAnalyzer : DiagnosticAnalyzer
{
    public const string ExpectedName = "context";

    private static readonly LocalizableString Title =
        "WidgetContext<T> extension receiver must be named 'context'";

    private static readonly LocalizableString MessageFormat =
        "Receiver parameter '{0}' of widget extension method '{1}' should be named 'context' (it is a WidgetContext<T>)";

    private static readonly LocalizableString Description =
        "Hex1b widget extensions on WidgetContext<T> use the receiver name 'context' to align with build-context semantics. Rename the receiver parameter to 'context'.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetContextReceiverName,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetContextReceiverName));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var widgetContextType = Hex1bSymbols.GetWidgetContextType(compilationStart.Compilation);
            if (widgetContextType is null)
            {
                return;
            }

            compilationStart.RegisterSymbolAction(
                ctx => AnalyzeMethod(ctx, widgetContextType),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol widgetContextType)
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
        if (!Hex1bSymbols.IsWidgetContext(receiver.Type, widgetContextType))
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
