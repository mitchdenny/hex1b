namespace Hex1b.Automation;

/// <summary>Declares a required executable without searching for it.</summary>
/// <param name="Program">One literal executable name.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeRequireCommand(string Program, TapeSourceSpan Span) : TapeCommand(Span);
