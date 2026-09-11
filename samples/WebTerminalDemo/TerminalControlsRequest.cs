namespace WebTerminalDemo;

internal sealed record TerminalControlsRequest(bool? Paused = null, int? Rate = null, int? Batch = null);
