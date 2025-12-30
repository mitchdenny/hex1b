namespace Hex1b.Tokens;

/// <summary>
/// Captures a cell that was modified during token application.
/// </summary>
/// <param name="X">The column position of the impacted cell (0-based).</param>
/// <param name="Y">The row position of the impacted cell (0-based).</param>
/// <param name="Cell">The cell state after the modification.</param>
public readonly record struct CellImpact(int X, int Y, TerminalCell Cell);
