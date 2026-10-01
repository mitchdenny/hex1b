namespace Hex1b.Tokens;

/// <summary>
/// Identifies a terminal graphics state change reported to presentation adapters.
/// </summary>
public enum TerminalGraphicsImpactKind
{
    /// <summary>
    /// A Sixel placement was added or replaced in the affected region.
    /// </summary>
    SixelAdded,

    /// <summary>
    /// Sixel pixels in the affected region were destructively damaged.
    /// </summary>
    SixelDamaged,
}
