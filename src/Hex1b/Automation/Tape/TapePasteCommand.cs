namespace Hex1b.Automation;

/// <summary>Declares a paste instruction.</summary>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapePasteCommand(TapeSourceSpan Span) : TapeCommand(Span);
