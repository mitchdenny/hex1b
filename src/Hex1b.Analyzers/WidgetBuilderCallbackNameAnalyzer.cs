using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0008: When a widget extension/instance method declares exactly one widget-builder
/// callback parameter (a <c>Func&lt;TContext, Hex1bWidget&gt;</c>-shaped parameter), it must
/// be named <c>builder</c>. Methods with two or more such callbacks are governed by HEX1B0009
/// instead and bypass this check.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetBuilderCallbackNameAnalyzer : DiagnosticAnalyzer
{
    public const string ExpectedName = "builder";

    private static readonly LocalizableString Title =
        "Widget-builder callback parameter must be named 'builder'";

    private static readonly LocalizableString MessageFormat =
        "Widget-builder callback parameter '{0}' on method '{1}' should be named 'builder'";

    private static readonly LocalizableString Description =
        "When a Hex1b widget extension or instance method takes exactly one widget-building callback (Func<TContext, Hex1bWidget> and friends), name it 'builder' for consistency with the rest of the API. Multi-builder methods are governed by HEX1B0009.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetBuilderCallbackName,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetBuilderCallbackName));

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

        // 'On*' methods are event-handler decorators (e.g. OnBlock, OnExpanding, OnClick).
        // Their callback parameter is semantically a "handler", not a widget-tree builder,
        // even when the handler returns a widget shape.
        if (IsEventHandlerMethodName(method.Name))
        {
            return;
        }

        if (!IsWidgetExtensionOrInstanceMethod(method, widgetType, widgetContextType))
        {
            return;
        }

        // Find all widget-builder callback parameters (skip the receiver of an extension method).
        var startIndex = method.IsExtensionMethod ? 1 : 0;
        IParameterSymbol? sole = null;
        var count = 0;
        for (var i = startIndex; i < method.Parameters.Length; i++)
        {
            var p = method.Parameters[i];
            if (Hex1bSymbols.IsWidgetBuilderCallback(p, widgetType))
            {
                count++;
                if (count == 1)
                {
                    sole = p;
                }
                else
                {
                    // HEX1B0009 owns multi-builder shape.
                    return;
                }
            }
        }

        if (count != 1 || sole is null)
        {
            return;
        }

        if (string.Equals(sole.Name, ExpectedName, System.StringComparison.Ordinal))
        {
            return;
        }

        var location = sole.Locations.IsDefaultOrEmpty ? Location.None : sole.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, sole.Name, method.Name));
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

            // WidgetContext<T> extensions are widget-tree builders.
            if (Hex1bSymbols.IsWidgetContext(receiverType, widgetContextType))
            {
                return true;
            }

            return Hex1bSymbols.IsWidgetTypeOrConstrainedToWidget(receiverType, widgetType);
        }

        // Instance methods declared on a widget type (e.g. methods on a widget record).
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
    /// Returns true if <paramref name="name"/> matches the Hex1b 'On*' event-handler convention
    /// (e.g. <c>OnClick</c>, <c>OnBlock</c>, <c>OnExpanding</c>). Such methods take handler
    /// callbacks rather than widget-tree builders.
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
