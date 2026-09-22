namespace Hex1b.Markdown;

/// <summary>
/// Column alignment for GFM table columns.
/// </summary>
public enum TableColumnAlignment
{
    /// <summary>No explicit alignment (default left).</summary>
    None,
    /// <summary>Left-aligned (:---).</summary>
    Left,
    /// <summary>Center-aligned (:---:).</summary>
    Center,
    /// <summary>Right-aligned (---:).</summary>
    Right,
}
