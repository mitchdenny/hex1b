namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI cursor position command (CUP): ESC [ row ; col H
/// </summary>
/// <param name="Row">1-based row number. Default is 1.</param>
/// <param name="Column">1-based column number. Default is 1.</param>
/// <remarks>
/// <para>
/// ANSI cursor positions are 1-based, where (1,1) is the top-left corner.
/// This matches the raw ANSI format for easier serialization.
/// </para>
/// <para>
/// When serialized, omitted parameters use ANSI defaults:
/// <list type="bullet">
///   <item>ESC[H → (1,1)</item>
///   <item>ESC[5H → (5,1)</item>
///   <item>ESC[;10H → (1,10)</item>
///   <item>ESC[5;10H → (5,10)</item>
/// </list>
/// </para>
/// </remarks>
public sealed record CursorPositionToken(int Row = 1, int Column = 1) : AnsiToken;
