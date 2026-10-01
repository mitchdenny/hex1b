namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Scroll Up (SU) command: ESC [ n S
/// Scrolls the content up by n lines, inserting blank lines at the bottom.
/// </summary>
/// <param name="Count">Number of lines to scroll. Default is 1.</param>
public sealed record ScrollUpToken(int Count = 1) : AnsiToken;
