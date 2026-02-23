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

/// <summary>
/// Specifies how a floated widget aligns vertically relative to its anchor.
/// </summary>
public enum FloatVerticalAlignment
{
    /// <summary>No vertical alignment — use absolute Y.</summary>
    None,
    /// <summary>Float's top edge aligns with anchor's top edge.</summary>
    AlignTop,
    /// <summary>Float's bottom edge aligns with anchor's bottom edge.</summary>
    AlignBottom,
    /// <summary>Float's top edge aligns with anchor's bottom edge (place below).</summary>
    ExtendBottom,
    /// <summary>Float's bottom edge aligns with anchor's top edge (place above).</summary>
    ExtendTop,
}
