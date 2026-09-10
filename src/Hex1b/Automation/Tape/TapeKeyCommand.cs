namespace Hex1b.Automation;

/// <summary>Presses a named key or scrolls, with optional repeat and delay operands.</summary>
/// <param name="Key">The case-sensitive Tape key name.</param>
/// <param name="Count">The explicit repeat count, or null for one repetition.</param>
/// <param name="Delay">The explicit delay, or null for the runtime default.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeKeyCommand(string Key, TapeNumberLiteral? Count, TapeDurationLiteral? Delay, TapeSourceSpan Span) : TapeCommand(Span);
