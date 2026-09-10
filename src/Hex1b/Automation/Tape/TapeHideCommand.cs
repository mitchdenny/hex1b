namespace Hex1b.Automation;

/// <summary>Declares the beginning of hidden output.</summary>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeHideCommand(TapeSourceSpan Span) : TapeCommand(Span);
