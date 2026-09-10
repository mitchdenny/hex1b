namespace Hex1b.Automation;

/// <summary>Retains a normalized setting operand and its original source spelling.</summary>
/// <remarks>A syntactically accepted value need not be a valid runtime number, duration, or JSON object.</remarks>
/// <param name="Value">The normalized, unconverted value.</param>
/// <param name="RawText">The original source spelling.</param>
/// <param name="Kind">The first operand token's lexical category.</param>
/// <param name="Span">The operand's source range.</param>
public sealed record TapeLiteral(string Value, string RawText, TapeTokenKind Kind, TapeSourceSpan Span);
