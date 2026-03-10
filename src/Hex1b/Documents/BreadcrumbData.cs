namespace Hex1b.Documents;

/// <summary>
/// Hierarchical document symbols for breadcrumb/outline navigation.
/// Each symbol has a name, kind, range, and optional children.
/// </summary>
/// <param name="Symbols">The top-level symbols in the document.</param>
public record BreadcrumbData(IReadOnlyList<BreadcrumbSymbol> Symbols);

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

/// <summary>
/// Symbol kinds for breadcrumb display.
/// </summary>
public enum BreadcrumbSymbolKind
{
    File, Module, Namespace, Package, Class, Method, Property,
    Field, Constructor, Enum, Interface, Function, Variable,
    Constant, String, Number, Boolean, Array, Object, Key, Null,
    EnumMember, Struct, Event, Operator, TypeParameter
}
