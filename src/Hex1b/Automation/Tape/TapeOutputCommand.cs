namespace Hex1b.Automation;

/// <summary>Declares an output path without creating an artifact.</summary>
/// <param name="Path">The output file or directory path.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeOutputCommand(string Path, TapeSourceSpan Span) : TapeCommand(Span);
