namespace Hex1b.Automation;

/// <summary>Retains a duration without requiring runtime conversion.</summary>
/// <param name="Value">The normalized value, including an explicit unit.</param>
/// <param name="RawText">The source spelling, excluding a preceding at sign.</param>
/// <param name="Span">The duration's source range.</param>
public sealed record TapeDurationLiteral(string Value, string RawText, TapeSourceSpan Span);
