using System.Collections;

namespace Hex1b.Documents;

/// <summary>
/// Single cursor state within a snapshot.
/// </summary>
public sealed record CursorSnapshotEntry(
    DocumentOffset Position,
    DocumentOffset? SelectionAnchor);
