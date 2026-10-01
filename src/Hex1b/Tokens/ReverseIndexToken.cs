namespace Hex1b.Tokens;

/// <summary>
/// Represents a Reverse Index (RI) command: ESC M
/// Moves the cursor up one line, scrolling if at the top margin.
/// </summary>
public sealed record ReverseIndexToken : AnsiToken
{
    /// <summary>Singleton instance.</summary>
    public static readonly ReverseIndexToken Instance = new();
    private ReverseIndexToken() { }
}
