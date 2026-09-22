namespace Hex1b.Tokens;

/// <summary>
/// Represents an Index (IND) command: ESC D
/// Moves the cursor down one line, scrolling if at the bottom margin.
/// </summary>
public sealed record IndexToken : AnsiToken
{
    /// <summary>Singleton instance.</summary>
    public static readonly IndexToken Instance = new();
    private IndexToken() { }
}
