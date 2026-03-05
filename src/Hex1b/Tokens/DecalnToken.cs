namespace Hex1b.Tokens;

/// <summary>
/// Represents the DECALN (DEC Screen Alignment Test) escape sequence: ESC # 8.
/// Fills the entire screen with 'E' characters and resets margins.
/// </summary>
public sealed record DecalnToken : AnsiToken
{
    public static readonly DecalnToken Instance = new();
    private DecalnToken() { }
}
