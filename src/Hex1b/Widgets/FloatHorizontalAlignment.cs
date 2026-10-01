namespace Hex1b.Widgets;

/// <summary>
/// Specifies how a floated widget aligns horizontally relative to its anchor.
/// </summary>
public enum FloatHorizontalAlignment
{
    /// <summary>No horizontal alignment — use absolute X.</summary>
    None,
    /// <summary>Float's left edge aligns with anchor's left edge.</summary>
    AlignLeft,
    /// <summary>Float's right edge aligns with anchor's right edge.</summary>
    AlignRight,
    /// <summary>Float's left edge aligns with anchor's right edge (place beside, to the right).</summary>
    ExtendRight,
    /// <summary>Float's right edge aligns with anchor's left edge (place beside, to the left).</summary>
    ExtendLeft,
}
