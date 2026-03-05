namespace Hex1b.Tokens;

/// <summary>
/// Represents a Tab Clear (TBC) sequence: CSI Ps g
/// </summary>
/// <param name="Mode">0 = clear tab at cursor, 3 = clear all tabs</param>
public sealed record TabClearToken(int Mode) : AnsiToken
{
    /// <summary>Clear tab stop at current cursor position.</summary>
    public static readonly TabClearToken AtCursor = new(0);
    
    /// <summary>Clear all tab stops.</summary>
    public static readonly TabClearToken All = new(3);
}
