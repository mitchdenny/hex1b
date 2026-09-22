namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Cursor Horizontal Absolute (CHA) command: ESC [ n G
/// </summary>
/// <param name="Column">1-based column number. Default is 1.</param>
/// <remarks>
/// <para>
/// Moves the cursor to column n in the current row.
/// </para>
/// </remarks>
public sealed record CursorColumnToken(int Column = 1) : AnsiToken;
