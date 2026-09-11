namespace Hex1b;

internal sealed record Hwt1SelectionState(
    long RequestId, string Status, string Mode, List<Hwt1SelectionRange> Ranges, string? Text);
