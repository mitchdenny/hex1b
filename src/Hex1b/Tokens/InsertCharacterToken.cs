namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Insert Character (ICH) command: ESC [ n @
/// Inserts n blank characters at cursor, shifting existing characters right.
/// </summary>
/// <param name="Count">Number of characters to insert. Default is 1.</param>
public sealed record InsertCharacterToken(int Count = 1) : AnsiToken;
