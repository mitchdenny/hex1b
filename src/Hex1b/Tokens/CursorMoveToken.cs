namespace Hex1b.Tokens;

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
