using System.Collections;

namespace Hex1b.Documents;

/// <summary>
/// Immutable snapshot of cursor positions for undo/redo.
/// </summary>
public sealed record CursorSetSnapshot(
    IReadOnlyList<CursorSnapshotEntry> Entries,
    int PrimaryIndex);
