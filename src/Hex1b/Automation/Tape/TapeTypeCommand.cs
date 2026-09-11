namespace Hex1b.Automation;

/// <summary>Types text using an optional per-character delay.</summary>
/// <param name="Text">The text, with consecutive string tokens joined by spaces.</param>
/// <param name="Delay">The explicit delay, or null to use the runtime default.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeTypeCommand(string Text, TapeDurationLiteral? Delay, TapeSourceSpan Span) : TapeCommand(Span);
