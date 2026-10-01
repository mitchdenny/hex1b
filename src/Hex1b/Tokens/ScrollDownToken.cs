namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Scroll Down (SD) command: ESC [ n T
/// Scrolls the content down by n lines, inserting blank lines at the top.
/// </summary>
/// <param name="Count">Number of lines to scroll. Default is 1.</param>
public sealed record ScrollDownToken(int Count = 1) : AnsiToken;
