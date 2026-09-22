namespace Hex1b.Documents;

/// <summary>
/// Hierarchical document symbols for breadcrumb/outline navigation.
/// Each symbol has a name, kind, range, and optional children.
/// </summary>
/// <param name="Symbols">The top-level symbols in the document.</param>
public record BreadcrumbData(IReadOnlyList<BreadcrumbSymbol> Symbols);
