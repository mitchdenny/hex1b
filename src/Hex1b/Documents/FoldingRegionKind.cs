namespace Hex1b.Documents;

/// <summary>
/// Predefined folding region kinds.
/// </summary>
public enum FoldingRegionKind
{
    /// <summary>A generic foldable region.</summary>
    Region,

    /// <summary>A comment block.</summary>
    Comment,

    /// <summary>An import/using block.</summary>
    Imports
}
