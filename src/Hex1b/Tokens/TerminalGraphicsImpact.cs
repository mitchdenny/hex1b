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

/// <summary>
/// Captures a terminal graphics placement or damage region.
/// </summary>
/// <param name="Kind">The kind of graphics change.</param>
/// <param name="X">The left column of the affected region.</param>
/// <param name="Y">The top row of the affected region.</param>
/// <param name="Width">The affected width in columns.</param>
/// <param name="Height">The affected height in rows.</param>
public readonly record struct TerminalGraphicsImpact(
    TerminalGraphicsImpactKind Kind,
    int X,
    int Y,
    int Width,
    int Height);
