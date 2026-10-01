namespace Hex1b.Tokens;

/// <summary>Sets a horizontal tab stop at the cursor column (HTS, ESC H).</summary>
public sealed record TabSetToken : AnsiToken
{
    /// <summary>Gets the shared horizontal tab-set token.</summary>
    public static readonly TabSetToken Instance = new();
}
