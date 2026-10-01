namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Erase Character (ECH) command: ESC [ n X
/// Erases n characters from cursor without moving cursor or shifting.
/// </summary>
/// <param name="Count">Number of characters to erase. Default is 1.</param>
public sealed record EraseCharacterToken(int Count = 1) : AnsiToken;
