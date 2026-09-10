namespace WebTerminalDemo;

internal sealed record TerminalTapeStatus(
    string TapeId, string Name, string State, DateTimeOffset StartedAt,
    int? CompletedCommands, string? Error, IReadOnlyList<string> Diagnostics);
