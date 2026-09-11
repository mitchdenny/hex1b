namespace Hex1b.Automation;

/// <summary>Declares a source include without opening or expanding the referenced file.</summary>
/// <param name="Path">The source path ending in .tape.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeSourceCommand(string Path, TapeSourceSpan Span) : TapeCommand(Span);
