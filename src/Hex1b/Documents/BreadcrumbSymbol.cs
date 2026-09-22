namespace Hex1b.Documents;

/// <summary>
/// A single symbol in the breadcrumb hierarchy.
/// </summary>
/// <param name="Name">The symbol's display name.</param>
/// <param name="Kind">The kind of symbol (class, method, etc.).</param>
/// <param name="Start">The start position of the document range this symbol spans.</param>
/// <param name="End">The end position of the document range this symbol spans.</param>
/// <param name="Children">Nested child symbols.</param>
public record BreadcrumbSymbol(
    string Name,
    BreadcrumbSymbolKind Kind,
    DocumentPosition Start,
    DocumentPosition End,
    IReadOnlyList<BreadcrumbSymbol>? Children = null);
