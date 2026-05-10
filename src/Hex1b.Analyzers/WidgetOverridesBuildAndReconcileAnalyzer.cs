using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0010: A widget must override either <c>Build</c> (compositional path) or
/// <c>ReconcileAsync</c> (primitive path), but not both. When both are overridden,
/// the runtime always honours <c>ReconcileAsync</c> and the <c>Build</c> override is
/// dead code that will silently never run.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetOverridesBuildAndReconcileAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Widget must not override both Build and ReconcileAsync";

    private static readonly LocalizableString MessageFormat =
        "Widget '{0}' overrides both 'Build' and 'ReconcileAsync'. The runtime always honours 'ReconcileAsync' on the primitive path; the 'Build' override is dead code. Choose one path.";

    private static readonly LocalizableString Description =
        "A Hex1b widget should pick exactly one authoring path. Override Build to author a widget compositionally — by returning a tree of other widgets — or override ReconcileAsync (and GetExpectedNodeType) to wire up a custom Hex1bNode. Overriding both leaves the Build override unreachable, hiding subtle bugs from authors who later expect Build to run.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetOverridesBuildAndReconcile,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetOverridesBuildAndReconcile));

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

            compilationStart.RegisterSymbolAction(
                ctx => AnalyzeNamedType(ctx, widgetType),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol widgetType)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind is not TypeKind.Class)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(type, widgetType))
        {
            return;
        }

        if (!Hex1bSymbols.InheritsFromOrEquals(type, widgetType))
        {
            return;
        }

        var overridesBuild = false;
        var overridesReconcile = false;

        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method || !method.IsOverride)
            {
                continue;
            }

            // Walk overrides up the chain to see if the original definition is on Hex1bWidget.
            if (method.Name == "Build" && OverridesMemberOnWidget(method, widgetType))
            {
                overridesBuild = true;
            }
            else if (method.Name == "ReconcileAsync" && OverridesMemberOnWidget(method, widgetType))
            {
                overridesReconcile = true;
            }

            if (overridesBuild && overridesReconcile)
            {
                break;
            }
        }

        if (!overridesBuild || !overridesReconcile)
        {
            return;
        }

        var location = type.Locations.IsDefaultOrEmpty ? Location.None : type.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }

    private static bool OverridesMemberOnWidget(IMethodSymbol method, INamedTypeSymbol widgetType)
    {
        for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current.ContainingType, widgetType))
            {
                return true;
            }
        }

        return SymbolEqualityComparer.Default.Equals(method.ContainingType, widgetType);
    }
}
