namespace Hex1b.Automation;

/// <summary>Declares the resumption of visible output.</summary>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeShowCommand(TapeSourceSpan Span) : TapeCommand(Span);
