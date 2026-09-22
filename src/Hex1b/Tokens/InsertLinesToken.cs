namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Insert Line (IL) command: ESC [ n L
/// Inserts n blank lines at the cursor position, pushing existing lines down.
/// </summary>
/// <param name="Count">Number of lines to insert. Default is 1.</param>
public sealed record InsertLinesToken(int Count = 1) : AnsiToken;
