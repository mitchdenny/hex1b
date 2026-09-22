namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Repeat (REP) command: ESC [ n b
/// Repeats the previous graphic character n times.
/// </summary>
/// <param name="Count">Number of times to repeat. Default is 1.</param>
public sealed record RepeatCharacterToken(int Count = 1) : AnsiToken;
