namespace Hex1b.Automation;

/// <summary>Waits for a regular expression to match terminal text.</summary>
/// <param name="Scope">The text region to examine.</param>
/// <param name="Pattern">The explicit pattern, including an empty pattern, or null for the runtime default.</param>
/// <param name="Timeout">The explicit timeout, or null for the runtime default.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeWaitCommand(TapeWaitScope Scope, string? Pattern, TapeDurationLiteral? Timeout, TapeSourceSpan Span) : TapeCommand(Span);
