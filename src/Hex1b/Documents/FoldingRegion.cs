namespace Hex1b.Documents;

/// <summary>
/// A collapsible code region that can be folded/unfolded.
/// </summary>
/// <param name="StartLine">The 1-based line where the region starts.</param>
/// <param name="EndLine">The 1-based line where the region ends.</param>
/// <param name="Kind">The kind of folding region.</param>
public record FoldingRegion(
    int StartLine,
    int EndLine,
    FoldingRegionKind Kind = FoldingRegionKind.Region)
{
    /// <summary>Whether the region is currently collapsed.</summary>
    public bool IsCollapsed { get; init; }
}

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
