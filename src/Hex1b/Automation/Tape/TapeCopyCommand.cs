namespace Hex1b.Automation;

/// <summary>Declares text to copy without accessing a clipboard.</summary>
/// <param name="Text">The text, with consecutive string tokens joined by spaces.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeCopyCommand(string Text, TapeSourceSpan Span) : TapeCommand(Span);
