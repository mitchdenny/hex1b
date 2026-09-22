namespace Hex1b.Tokens;

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
