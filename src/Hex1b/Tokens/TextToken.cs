namespace Hex1b.Tokens;

/// <summary>
/// Represents plain text content to be written at the current cursor position.
/// </summary>
/// <param name="Text">
/// The text content, which may include grapheme clusters (emoji, combining characters, etc.).
/// This is never empty - empty text should not generate a token.
/// </param>
/// <remarks>
/// When serialized, this emits the raw text. The terminal will write it at the current
/// cursor position using the current SGR attributes.
/// </remarks>
public sealed record TextToken(string Text) : AnsiToken;
