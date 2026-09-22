namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Delete Line (DL) command: ESC [ n M
/// Deletes n lines at the cursor position, pulling lines up from below.
/// </summary>
/// <param name="Count">Number of lines to delete. Default is 1.</param>
public sealed record DeleteLinesToken(int Count = 1) : AnsiToken;
