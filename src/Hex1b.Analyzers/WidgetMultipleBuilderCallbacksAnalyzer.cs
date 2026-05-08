using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0009: A widget extension/instance method should declare at most one widget-builder
/// callback parameter. Methods that genuinely need multiple builders (e.g. a splitter with
/// left/right panes) must suppress this rule with an explicit justification, signalling that
/// the multi-builder shape was a deliberate choice rather than an oversight.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetMultipleBuilderCallbacksAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Widget extension/instance method should declare at most one widget-builder callback";

    private static readonly LocalizableString MessageFormat =
        "Method '{0}' declares {1} widget-builder callback parameters; prefer a single 'builder' callback or a context type with named slots. Suppress this diagnostic with a justification if the multi-builder shape is intentional.";

    private static readonly LocalizableString Description =
        "Hex1b widget APIs prefer a single 'builder' callback (or a context type with named slots like SplitterContext) over multiple sibling Func<...> parameters. When a multi-builder shape is genuinely needed, suppress HEX1B0009 explicitly to record the decision.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetMultipleBuilderCallbacks,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetMultipleBuilderCallbacks));

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

        if (method.IsOverride || !method.ExplicitInterfaceImplementations.IsDefaultOrEmpty)
        {
            return;
        }

        // 'On*' methods are event-handler decorators; their multiple Func parameters are
        // sibling handlers, not multiple widget-tree builders.
        if (IsEventHandlerMethodName(method.Name))
        {
            return;
        }

        if (!IsWidgetExtensionOrInstanceMethod(method, widgetType, widgetContextType))
        {
            return;
        }

        var startIndex = method.IsExtensionMethod ? 1 : 0;
        var count = 0;
        for (var i = startIndex; i < method.Parameters.Length; i++)
        {
            if (Hex1bSymbols.IsWidgetBuilderCallback(method.Parameters[i], widgetType))
            {
                count++;
            }
        }

        if (count < 2)
        {
            return;
        }

        var location = method.Locations.IsDefaultOrEmpty ? Location.None : method.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name, count));
    }

    private static bool IsWidgetExtensionOrInstanceMethod(
        IMethodSymbol method,
        INamedTypeSymbol widgetType,
        INamedTypeSymbol? widgetContextType)
    {
        if (method.IsExtensionMethod)
        {
            if (method.Parameters.IsDefaultOrEmpty)
            {
                return false;
            }

            var receiverType = method.Parameters[0].Type;

            if (Hex1bSymbols.IsWidgetContext(receiverType, widgetContextType))
            {
                return true;
            }

            return Hex1bSymbols.IsWidgetTypeOrConstrainedToWidget(receiverType, widgetType);
        }

        if (method.IsStatic)
        {
            return false;
        }

        if (method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            return false;
        }

        if (method.MethodKind is MethodKind.Constructor
            or MethodKind.PropertyGet
            or MethodKind.PropertySet
            or MethodKind.EventAdd
            or MethodKind.EventRemove)
        {
            return false;
        }

        return Hex1bSymbols.InheritsFromOrEquals(method.ContainingType, widgetType);
    }

    /// <summary>
    /// Returns true if <paramref name="name"/> matches the Hex1b 'On*' event-handler convention.
    /// Such methods host handler callbacks rather than widget-tree builders.
    /// </summary>
    private static bool IsEventHandlerMethodName(string name)
    {
        if (name.Length < 3)
        {
            return false;
        }

        return name[0] == 'O' && name[1] == 'n' && char.IsUpper(name[2]);
    }
}
