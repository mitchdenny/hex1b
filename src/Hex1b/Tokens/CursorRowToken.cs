namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Vertical Position Absolute (VPA) command: ESC [ n d
/// </summary>
/// <param name="Row">1-based row number. Default is 1.</param>
/// <remarks>
/// <para>
/// Moves the cursor to row n in the current column.
/// Also known as Line Position Absolute (LPA).
/// </para>
/// </remarks>
public sealed record CursorRowToken(int Row = 1) : AnsiToken;
