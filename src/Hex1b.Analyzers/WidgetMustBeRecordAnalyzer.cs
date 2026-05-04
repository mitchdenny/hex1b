using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0004: Types deriving from <c>Hex1bWidget</c> must be declared as <c>record</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WidgetMustBeRecordAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Widget type must be declared as a record";

    private static readonly LocalizableString MessageFormat =
        "Widget type '{0}' must be declared as 'record' to preserve immutability across reconciliation";

    private static readonly LocalizableString Description =
        "Hex1b widgets are immutable configuration objects that drive reconciliation. Declaring a widget as 'class' instead of 'record' breaks 'with'-expression cloning and value equality, both of which the reconciler relies on.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.WidgetMustBeRecord,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.WidgetMustBeRecord));

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

        if (type.IsRecord)
        {
            return;
        }

        var location = type.Locations.IsDefaultOrEmpty ? Location.None : type.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }
}
