namespace Hex1b.Automation;

/// <summary>Declares an environment variable without changing the process environment.</summary>
/// <param name="Name">The unvalidated environment variable name.</param>
/// <param name="Value">The literal value, without variable expansion.</param>
/// <param name="Span">The instruction's source range.</param>
public sealed record TapeEnvCommand(string Name, string Value, TapeSourceSpan Span) : TapeCommand(Span);
