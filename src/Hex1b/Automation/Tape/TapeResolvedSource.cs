namespace Hex1b.Automation;

internal sealed record TapeResolvedSource(
    string? SourceName,
    string WorkingDirectory,
    IReadOnlyList<TapeCommand> Commands,
    IReadOnlyList<TapeDiagnostic> Diagnostics);
