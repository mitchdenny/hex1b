namespace Hex1b.Automation;

/// <summary>Pauses execution for a duration.</summary>
/// <param name="Duration">The unconverted duration.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeSleepCommand(TapeDurationLiteral Duration, TapeSourceSpan Span) : TapeCommand(Span);
