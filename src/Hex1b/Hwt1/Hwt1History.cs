namespace Hex1b;

internal sealed record Hwt1History(
    string Generation, string Buffer, int TotalRows, int Top, int LiveTop,
    bool Following, string[] RowIds, long RequestId,
    Hwt1SelectionState Selection, Hwt1CopyState? Copy)
{
    public Hwt1Marker[] Markers { get; init; } = [];
    public Hwt1MarkerResult? MarkerResult { get; init; }
    public Hwt1MarkerPage? MarkerPage { get; init; }
    public string? ViewportError { get; init; }
}
