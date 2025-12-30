namespace Hex1b.Tokens;

/// <summary>
/// The mode for CSI clear screen (ED) and clear line (EL) commands.
/// </summary>
public enum ClearMode
{
    /// <summary>Clear from cursor to end (of screen or line).</summary>
    ToEnd = 0,
    
    /// <summary>Clear from start (of screen or line) to cursor.</summary>
    ToStart = 1,
    
    /// <summary>Clear entire (screen or line).</summary>
    All = 2,
    
    /// <summary>Clear entire screen and scrollback buffer (ED only).</summary>
    AllAndScrollback = 3
}
