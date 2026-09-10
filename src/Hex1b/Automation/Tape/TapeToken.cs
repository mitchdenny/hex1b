namespace Hex1b.Automation;

/// <summary>Represents a source token without escape decoding.</summary>
/// <param name="Kind">The lexical category.</param>
/// <param name="Value">The token value with delimiters removed where applicable.</param>
/// <param name="RawText">The original token spelling, including delimiters.</param>
/// <param name="Span">The token's source range.</param>
public sealed record TapeToken(TapeTokenKind Kind, string Value, string RawText, TapeSourceSpan Span);
