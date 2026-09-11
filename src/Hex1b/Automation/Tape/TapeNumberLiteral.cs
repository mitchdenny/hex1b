namespace Hex1b.Automation;

/// <summary>Retains a numeric token, including values whose numeric conversion would fail.</summary>
/// <param name="Value">The unconverted number spelling.</param>
/// <param name="RawText">The original source spelling.</param>
/// <param name="Span">The number's source range.</param>
public sealed record TapeNumberLiteral(string Value, string RawText, TapeSourceSpan Span);
