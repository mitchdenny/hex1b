namespace Hex1b.Tokens;

/// <summary>
/// Direction for relative cursor movement.
/// </summary>
public enum CursorMoveDirection
{
    /// <summary>Cursor Up (CUU) - ESC [ n A</summary>
    Up,
    /// <summary>Cursor Down (CUD) - ESC [ n B</summary>
    Down,
    /// <summary>Cursor Forward/Right (CUF) - ESC [ n C</summary>
    Forward,
    /// <summary>Cursor Back/Left (CUB) - ESC [ n D</summary>
    Back,
    /// <summary>Cursor Next Line (CNL) - ESC [ n E - move to beginning of line n lines down</summary>
    NextLine,
    /// <summary>Cursor Previous Line (CPL) - ESC [ n F - move to beginning of line n lines up</summary>
    PreviousLine
}

/// <summary>
/// Represents a CSI cursor movement command.
/// </summary>
/// <param name="Direction">The direction to move.</param>
/// <param name="Count">Number of cells/lines to move. Default is 1.</param>
/// <remarks>
/// <para>
/// Covers the following ANSI sequences:
/// <list type="bullet">
///   <item>ESC [ n A - Cursor Up (CUU)</item>
///   <item>ESC [ n B - Cursor Down (CUD)</item>
///   <item>ESC [ n C - Cursor Forward (CUF)</item>
///   <item>ESC [ n D - Cursor Back (CUB)</item>
///   <item>ESC [ n E - Cursor Next Line (CNL)</item>
///   <item>ESC [ n F - Cursor Previous Line (CPL)</item>
/// </list>
/// </para>
/// <para>
/// If n is omitted or 0, it defaults to 1.
/// </para>
/// </remarks>
public sealed record CursorMoveToken(CursorMoveDirection Direction, int Count = 1) : AnsiToken;

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
