namespace Hex1b.Tokens;

/// <summary>
/// Represents a control character (CR, LF, TAB, etc.).
/// </summary>
/// <param name="Character">The control character (e.g., '\n', '\r', '\t').</param>
/// <remarks>
/// Control characters are kept separate from <see cref="TextToken"/> because they
/// have special cursor-movement semantics rather than writing visible content.
/// </remarks>
public sealed record ControlCharacterToken(char Character) : AnsiToken
{
    /// <summary>Carriage return - moves cursor to column 1.</summary>
    public static readonly ControlCharacterToken CarriageReturn = new('\r');
    
    /// <summary>Line feed - moves cursor down one row.</summary>
    public static readonly ControlCharacterToken LineFeed = new('\n');
    
    /// <summary>Tab - moves cursor to next tab stop.</summary>
    public static readonly ControlCharacterToken Tab = new('\t');
}
