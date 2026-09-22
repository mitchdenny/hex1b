namespace Hex1b.Widgets;

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
