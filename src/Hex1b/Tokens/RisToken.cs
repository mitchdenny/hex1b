namespace Hex1b.Tokens;

/// <summary>
/// Token for RIS (Reset to Initial State, ESC c) — full terminal reset.
/// </summary>
public sealed record RisToken : AnsiToken
{
    public static readonly RisToken Instance = new();
    private RisToken() { }
}
