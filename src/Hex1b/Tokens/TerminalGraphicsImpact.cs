namespace Hex1b.Tokens;

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
