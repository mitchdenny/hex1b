using Hex1b.Layout;

namespace Hex1b.Automation;

internal sealed class TapePreparedRun
{
    internal required string WorkingDirectory { get; init; }
    internal required IReadOnlyList<TapePreparedCommand> Commands { get; init; }
    internal required IReadOnlyList<TapeDiagnostic> Diagnostics { get; init; }
    internal required IReadOnlyList<TapeArtifact> Artifacts { get; init; }
    internal IReadOnlyList<string> InitiallyEnabledGoldenPaths { get; init; } = [];
    internal Size? TerminalSize { get; init; }
    internal bool Overwrite { get; init; }
    internal TapeShellConfiguration? Shell { get; init; }
    internal IReadOnlyDictionary<string, string>? Environment { get; init; }

    internal TapeValidationResult Validation => new(Diagnostics);
}
