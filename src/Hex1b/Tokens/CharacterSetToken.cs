namespace Hex1b.Tokens;

/// <summary>
/// Represents a character set designation sequence: ESC ( X or ESC ) X
/// These sequences select which character set to use for the G0 or G1 character set.
/// </summary>
/// <param name="Target">Which character set to designate: 0 for G0, 1 for G1.</param>
/// <param name="Charset">The character set identifier (e.g., '0' for line drawing, 'B' for ASCII).</param>
public sealed record CharacterSetToken(int Target, char Charset) : AnsiToken;

/// <summary>
/// Represents an Application Keypad Mode sequence: ESC =
/// Switches the keypad to application mode.
/// </summary>
public sealed record KeypadModeToken(bool Application) : AnsiToken
{
    /// <summary>Singleton for application keypad mode (ESC =).</summary>
    public static readonly KeypadModeToken ApplicationMode = new(true);
    
    /// <summary>Singleton for normal keypad mode (ESC >).</summary>
    public static readonly KeypadModeToken NormalMode = new(false);
}
