namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Delete Character (DCH) command: ESC [ n P
/// Deletes n characters at cursor, shifting remaining characters left.
/// </summary>
/// <param name="Count">Number of characters to delete. Default is 1.</param>
public sealed record DeleteCharacterToken(int Count = 1) : AnsiToken;
