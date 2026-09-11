namespace Hex1b.Automation;

/// <summary>Declares a screenshot instruction without creating a file.</summary>
/// <param name="Path">The PNG file path.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeScreenshotCommand(string Path, TapeSourceSpan Span) : TapeCommand(Span);
