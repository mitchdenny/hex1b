using System.Collections;

namespace Hex1b.Documents;

/// <summary>
/// A sorted, non-overlapping collection of document cursors.
/// Cursors are maintained in ascending order by position.
/// After multi-cursor operations, overlapping cursors are automatically merged.
/// </summary>
public class CursorSet : IReadOnlyList<DocumentCursor>
{
    private readonly List<DocumentCursor> _cursors = [];
    private int _primaryIndex;

    public CursorSet()
    {
        _cursors.Add(new DocumentCursor());
        _primaryIndex = 0;
    }

    /// <summary>The primary cursor (receives focus, used for single-cursor operations).</summary>
    public DocumentCursor Primary => _cursors[_primaryIndex];

    /// <summary>Index of the primary cursor within the sorted list.</summary>
    public int PrimaryIndex
    {
        get => _primaryIndex;
        internal set => _primaryIndex = Math.Clamp(value, 0, Math.Max(0, _cursors.Count - 1));
    }

    /// <summary>Number of cursors.</summary>
    public int Count => _cursors.Count;

    /// <summary>Access cursor by index (sorted order).</summary>
    public DocumentCursor this[int index] => _cursors[index];

    /// <summary>
    /// Add a new cursor at the given offset. The cursor is inserted in sorted order.
    /// Returns the index of the newly added cursor.
    /// </summary>
    public int Add(DocumentOffset position)
    {
        var cursor = new DocumentCursor { Position = position };
        return InsertSorted(cursor);
    }

    /// <summary>
    /// Add a new cursor with a selection. The cursor is inserted in sorted order.
    /// Returns the index of the newly added cursor.
    /// </summary>
    public int Add(DocumentOffset position, DocumentOffset? selectionAnchor)
    {
        var cursor = new DocumentCursor { Position = position, SelectionAnchor = selectionAnchor };
        return InsertSorted(cursor);
    }

    /// <summary>
    /// Remove all cursors except the primary.
    /// </summary>
    public void CollapseToSingle()
    {
        if (_cursors.Count <= 1) return;
        var primary = Primary;
        _cursors.Clear();
        _cursors.Add(primary);
        _primaryIndex = 0;
    }

    /// <summary>
    /// Merge overlapping or adjacent cursors, preserving the primary where possible.
    /// Call this after operations that may cause cursor positions to overlap.
    /// </summary>
    public void MergeOverlapping()
    {
        if (_cursors.Count <= 1) return;

        // Sort by position
        SortCursors();

        var primaryCursor = Primary;
        var merged = new List<DocumentCursor>(_cursors.Count);
        var current = _cursors[0];

        for (var i = 1; i < _cursors.Count; i++)
        {
            var next = _cursors[i];

            if (ShouldMerge(current, next))
            {
                current = MergeTwoCursors(current, next, primaryCursor);
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);

        _cursors.Clear();
        _cursors.AddRange(merged);

        // Restore primary index
        _primaryIndex = 0;
        for (var i = 0; i < _cursors.Count; i++)
        {
            if (ReferenceEquals(_cursors[i], primaryCursor))
            {
                _primaryIndex = i;
                break;
            }
        }
    }

    /// <summary>
    /// Iterate cursors in reverse document order (highest offset first).
    /// Use this when applying edits to avoid offset invalidation.
    /// </summary>
    public IEnumerable<(DocumentCursor Cursor, int Index)> InReverseOrder()
    {
        for (var i = _cursors.Count - 1; i >= 0; i--)
        {
            yield return (_cursors[i], i);
        }
    }

    /// <summary>
    /// Create a snapshot of all cursor positions and selections for undo/redo.
    /// </summary>
    public CursorSetSnapshot Snapshot()
    {
        var entries = new CursorSnapshotEntry[_cursors.Count];
        for (var i = 0; i < _cursors.Count; i++)
        {
            entries[i] = new CursorSnapshotEntry(_cursors[i].Position, _cursors[i].SelectionAnchor);
        }
        return new CursorSetSnapshot(entries, _primaryIndex);
    }

    /// <summary>
    /// Restore cursor positions from a snapshot.
    /// </summary>
    public void Restore(CursorSetSnapshot snapshot)
    {
        _cursors.Clear();
        foreach (var entry in snapshot.Entries)
        {
            _cursors.Add(new DocumentCursor
            {
                Position = entry.Position,
                SelectionAnchor = entry.SelectionAnchor
            });
        }
        _primaryIndex = Math.Clamp(snapshot.PrimaryIndex, 0, Math.Max(0, _cursors.Count - 1));
    }

    /// <summary>
    /// Clamp all cursors to valid document range.
    /// </summary>
    public void ClampAll(int documentLength)
    {
        foreach (var cursor in _cursors)
        {
            cursor.Clamp(documentLength);
        }
    }

    /// <summary>
    /// Re-sort cursors after position changes. 
    /// Tracks primary cursor through the sort.
    /// </summary>
    public void Sort()
    {
        SortCursors();
    }

    // ── IReadOnlyList implementation ─────────────────────────────

    public IEnumerator<DocumentCursor> GetEnumerator() => _cursors.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── Private helpers ──────────────────────────────────────────

    private int InsertSorted(DocumentCursor cursor)
    {
        var index = 0;
        while (index < _cursors.Count && _cursors[index].Position < cursor.Position)
        {
            index++;
        }
        _cursors.Insert(index, cursor);

        // Adjust primary index if insertion was before primary
        if (index <= _primaryIndex && _cursors.Count > 1)
        {
            _primaryIndex++;
        }

        return index;
    }

    private void SortCursors()
    {
        var primaryCursor = Primary;
        _cursors.Sort((a, b) => a.Position.CompareTo(b.Position));
        for (var i = 0; i < _cursors.Count; i++)
        {
            if (ReferenceEquals(_cursors[i], primaryCursor))
            {
                _primaryIndex = i;
                break;
            }
        }
    }

    private static bool ShouldMerge(DocumentCursor a, DocumentCursor b)
    {
        // Merge if cursors are at the same position or their selections overlap
        var aEnd = a.HasSelection ? a.SelectionEnd : a.Position;
        var bStart = b.HasSelection ? b.SelectionStart : b.Position;
        return aEnd >= bStart;
    }

    private static DocumentCursor MergeTwoCursors(
        DocumentCursor a, DocumentCursor b, DocumentCursor primaryCursor)
    {
        // Prefer the primary cursor's reference, otherwise keep the later one
        var keep = ReferenceEquals(a, primaryCursor) ? a :
                   ReferenceEquals(b, primaryCursor) ? b : b;

        // Merge selection: union of both ranges
        var minStart = Min(
            a.HasSelection ? a.SelectionStart : a.Position,
            b.HasSelection ? b.SelectionStart : b.Position);
        var maxEnd = Max(
            a.HasSelection ? a.SelectionEnd : a.Position,
            b.HasSelection ? b.SelectionEnd : b.Position);

        if (minStart != maxEnd)
        {
            // Keep selection direction of the surviving cursor
            if (keep.HasSelection && keep.SelectionAnchor!.Value <= keep.Position)
            {
                keep.SelectionAnchor = minStart;
                keep.Position = maxEnd;
            }
            else if (keep.HasSelection)
            {
                keep.SelectionAnchor = maxEnd;
                keep.Position = minStart;
            }
            else
            {
                // No selection direction preference — position at the end
                keep.Position = maxEnd;
            }
        }
        else
        {
            keep.Position = minStart;
            keep.ClearSelection();
        }

        return keep;
    }

    private static DocumentOffset Min(DocumentOffset a, DocumentOffset b) => a < b ? a : b;
    private static DocumentOffset Max(DocumentOffset a, DocumentOffset b) => a > b ? a : b;
}

/// <summary>
/// Immutable snapshot of cursor positions for undo/redo.
/// </summary>
public sealed record CursorSetSnapshot(
    IReadOnlyList<CursorSnapshotEntry> Entries,
    int PrimaryIndex);

/// <summary>
/// Single cursor state within a snapshot.
/// </summary>
public sealed record CursorSnapshotEntry(
    DocumentOffset Position,
    DocumentOffset? SelectionAnchor);
