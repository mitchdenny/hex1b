using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hex1b.Analyzers;

/// <summary>
/// HEX1B0011: Each source file should contain at most one distinct top-level type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneTopLevelTypePerFileAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: Hex1bDiagnosticIds.OneTopLevelTypePerFile,
        title: "Declare one top-level type per file",
        messageFormat: "Move top-level type '{0}' to its own file",
        category: Hex1bDiagnosticIds.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: false,
        description: "Each source file may declare only one distinct top-level type. Nested types, generated code, and multiple partial declarations of the same type are allowed.",
        helpLinkUri: Hex1bDiagnosticIds.HelpLink(Hex1bDiagnosticIds.OneTopLevelTypePerFile));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSemanticModelAction(AnalyzeFile);
    }

    private static void AnalyzeFile(SemanticModelAnalysisContext context)
    {
        var root = context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken);
        var types = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        // Descend through namespaces, but never into a type or its members.
        foreach (var node in root.DescendantNodes(
                     descendIntoChildren: node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            var identifier = node switch
            {
                BaseTypeDeclarationSyntax declaration => declaration.Identifier,
                DelegateDeclarationSyntax declaration => declaration.Identifier,
                _ => default
            };

            if (identifier == default ||
                context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not INamedTypeSymbol type ||
                !types.Add(type) ||
                types.Count == 1)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, identifier.GetLocation(), type.Name));
        }
    }
}
