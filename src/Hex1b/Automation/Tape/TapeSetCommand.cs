namespace Hex1b.Automation;

/// <summary>Declares a Tape setting without evaluating it.</summary>
/// <param name="Setting">The setting identity.</param>
/// <param name="Value">The lossless, normalized value.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeSetCommand(TapeSetting Setting, TapeLiteral Value, TapeSourceSpan Span) : TapeCommand(Span);
