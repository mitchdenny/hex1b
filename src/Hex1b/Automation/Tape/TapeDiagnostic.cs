namespace Hex1b.Automation;

/// <summary>Describes a problem encountered while parsing or executing a Tape document.</summary>
/// <param name="Code">The stable diagnostic identifier.</param>
/// <param name="Severity">The diagnostic severity.</param>
/// <param name="Stage">The stage that reported the diagnostic.</param>
/// <param name="Message">The human-readable explanation.</param>
/// <param name="Span">The associated source range.</param>
public sealed record TapeDiagnostic(string Code, TapeDiagnosticSeverity Severity, TapeDiagnosticStage Stage, string Message, TapeSourceSpan Span);
