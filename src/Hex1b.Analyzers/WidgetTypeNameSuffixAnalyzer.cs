using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0002: Types deriving from <c>Hex1bWidget</c> must have a name ending in <c>Widget</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetTypeNameSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Widget type name must end with 'Widget'";

    private static readonly LocalizableString MessageFormat =
        "Type '{0}' derives from Hex1bWidget but does not end with the 'Widget' suffix. Rename to '{0}Widget'.";

    private static readonly LocalizableString Description =
        "Hex1b widgets follow a strict naming convention so the API surface remains discoverable: every type that derives (directly or transitively) from Hex1bWidget must have a name ending in 'Widget'.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetTypeNameMissingSuffix,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetTypeNameMissingSuffix));

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

        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        // The base type itself ('Hex1bWidget') is named to satisfy the rule, but we skip it
        // explicitly so any future rename of the base would not flag the base symbol.
        if (SymbolEqualityComparer.Default.Equals(type, widgetType))
        {
            return;
        }

        if (!Hex1bSymbols.InheritsFromOrEquals(type, widgetType))
        {
            return;
        }

        if (type.Name.EndsWith("Widget", System.StringComparison.Ordinal))
        {
            return;
        }

        var location = type.Locations.IsDefaultOrEmpty ? Location.None : type.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }
}
