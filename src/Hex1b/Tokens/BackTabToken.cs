namespace Hex1b.Tokens;

/// <summary>
/// Represents a Backtab (Shift+Tab) escape sequence (CSI Z).
/// </summary>
/// <remarks>
/// The backtab sequence is sent by terminals when the user presses Shift+Tab.
/// It is translated to a Tab key event with the Shift modifier.
/// </remarks>
public sealed record BackTabToken : AnsiToken
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly BackTabToken Instance = new();
    
    private BackTabToken() { }
}
