using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0003: Types deriving from <c>Hex1bNode</c> must have a name ending in <c>Node</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NodeTypeNameSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableString Title =
        "Node type name must end with 'Node'";

    private static readonly LocalizableString MessageFormat =
        "Type '{0}' derives from Hex1bNode but does not end with the 'Node' suffix. Rename to '{0}Node'.";

    private static readonly LocalizableString Description =
        "Hex1b nodes follow a strict naming convention: every type that derives (directly or transitively) from Hex1bNode must have a name ending in 'Node'.";

    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.NodeTypeNameMissingSuffix,
        title: Title,
        messageFormat: MessageFormat,
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.NodeTypeNameMissingSuffix));

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

        if (type.Name.EndsWith("Node", System.StringComparison.Ordinal))
        {
            return;
        }

        var location = type.Locations.IsDefaultOrEmpty ? Location.None : type.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
    }
}
