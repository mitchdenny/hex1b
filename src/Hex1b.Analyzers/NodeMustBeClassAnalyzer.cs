using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0005: Types deriving from <c>Hex1bNode</c> must be declared as <c>class</c> (not <c>record</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NodeMustBeClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Node type must be declared as a class";

    private static readonly LocalizableString MessageFormat =
        "Node type '{0}' must be declared as 'class' rather than 'record'. Nodes carry mutable state that must be preserved across reconciliation by reference.";

    private static readonly LocalizableString Description =
        "Hex1b nodes own mutable layout, focus, and rendering state that must be preserved by reference across reconciliation passes. Declaring a node as 'record' would imply value equality and 'with'-expression cloning, both of which break the reconciler's identity assumptions.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.NodeMustBeClass,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.NodeMustBeClass));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var nodeType = Hex1bSymbols.GetNodeType(compilationStart.Compilation);
            if (nodeType is null)
            {
                return;
            }

            compilationStart.RegisterSymbolAction(
                ctx => AnalyzeNamedType(ctx, nodeType),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol nodeType)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind is not TypeKind.Class)
        {
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(type, nodeType))
        {
            return;
        }

        if (!Hex1bSymbols.InheritsFromOrEquals(type, nodeType))
        {
            return;
        }

        if (!type.IsRecord)
        {
            return;
        }

        var location = type.Locations.IsDefaultOrEmpty ? Location.None : type.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }
}
