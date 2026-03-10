namespace Hex1b.LanguageServer;

/// <summary>
/// Flags enum identifying individual LSP features that can be enabled/disabled
/// per language extension.
/// </summary>
[Flags]
public enum LspFeatureSet
{
    /// <summary>No features enabled.</summary>
    None = 0,

    /// <summary>Semantic token-based syntax highlighting.</summary>
    SemanticTokens = 1 << 0,

    /// <summary>Code completion suggestions.</summary>
    Completion = 1 << 1,

    /// <summary>Inline diagnostic markers.</summary>
    Diagnostics = 1 << 2,

    /// <summary>Hover information popups.</summary>
    Hover = 1 << 3,

    /// <summary>Go to definition navigation.</summary>
    Definition = 1 << 4,

    /// <summary>Find all references.</summary>
    References = 1 << 5,

    /// <summary>Symbol rename refactoring.</summary>
    Rename = 1 << 6,

    /// <summary>Function signature help.</summary>
    SignatureHelp = 1 << 7,

    /// <summary>Code actions (quick fixes, refactorings).</summary>
    CodeActions = 1 << 8,

    /// <summary>Document formatting.</summary>
    Formatting = 1 << 9,

    /// <summary>Document symbol outline.</summary>
    DocumentSymbol = 1 << 10,

    /// <summary>Highlight occurrences of the symbol under cursor.</summary>
    DocumentHighlight = 1 << 11,

    /// <summary>Code folding regions.</summary>
    FoldingRange = 1 << 12,

    /// <summary>Smart selection expansion.</summary>
    SelectionRange = 1 << 13,

    /// <summary>Inlay hints (parameter names, inferred types).</summary>
    InlayHints = 1 << 14,

    /// <summary>Code lens annotations.</summary>
    CodeLens = 1 << 15,

    /// <summary>Call hierarchy navigation.</summary>
    CallHierarchy = 1 << 16,

    /// <summary>Type hierarchy navigation.</summary>
    TypeHierarchy = 1 << 17,

    /// <summary>All features enabled.</summary>
    All = ~None,
}
