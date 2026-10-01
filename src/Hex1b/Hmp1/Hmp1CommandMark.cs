namespace Hex1b;

internal sealed record Hmp1CommandMark(
    long Id, bool Alternate, int Row, int Column,
    TerminalShellIntegrationPhase Phase, int? ExitCode, string? RawParameters);
