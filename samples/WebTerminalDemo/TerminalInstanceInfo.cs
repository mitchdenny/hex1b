namespace WebTerminalDemo;

internal sealed record TerminalInstanceInfo(
    string Id, string Name, string Scene, int Columns, int Rows,
    int PeerCount, string? PrimaryPeerId, DateTimeOffset CreatedAt,
    bool? Paused, int? Rate, int? Batch,
    IReadOnlyList<DemoTapeInfo> Tapes, TerminalTapeStatus? TapePlayback);
