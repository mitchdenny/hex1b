using Hex1b.Reflow;

namespace Hex1b;

internal enum ScrollbackPruneReason
{
    Capacity,
    Clear,
}

internal readonly record struct ScrollbackPrunedRow(
    long RowId,
    long? SuccessorRowId,
    ScrollbackPruneReason Reason);

internal readonly record struct ScrollbackEntry(
    long RowId,
    ScrollbackRow Row);

internal readonly record struct ScrollbackPushResult(
    long RowId,
    ScrollbackRow? EvictedRow,
    long? EvictedRowId,
    long? SuccessorRowId);

internal readonly record struct ScrollbackReplacementResult(
    ScrollbackEntry[] Entries,
    int DiscardedRowCount);

/// <summary>
/// A fixed-capacity circular buffer that stores terminal rows scrolled off screen.
/// </summary>
/// <remarks>
/// <para>
/// Rows are stored in insertion order. When the buffer is full, the oldest row is
/// evicted to make room for the new one. Tracked objects (Sixel data, hyperlinks)
/// are reference-counted: <see cref="TrackedObject{T}.AddRef"/> is called when a
/// row enters the buffer, and <see cref="TrackedObject{T}.Release"/> when it is
/// evicted or the buffer is cleared.
/// </para>
/// </remarks>
internal sealed class ScrollbackBuffer
{
    private readonly ScrollbackRow[] _rows;
    private readonly long[] _rowIds;
    private readonly Action<ScrollbackPrunedRow>? _rowPruned;
    private int _head; // Next write position
    private int _count;
    private long _nextRowId = 1;

    /// <summary>
    /// Creates a scrollback buffer with the specified maximum line capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of rows to retain.</param>
    public ScrollbackBuffer(int capacity)
        : this(capacity, rowPruned: null)
    {
    }

    internal ScrollbackBuffer(
        int capacity,
        Action<ScrollbackPrunedRow>? rowPruned)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _rows = new ScrollbackRow[capacity];
        _rowIds = new long[capacity];
        _rowPruned = rowPruned;
        Capacity = capacity;
    }

    /// <summary>
    /// Maximum number of rows this buffer can hold.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Number of rows currently stored.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds a row to the buffer. If the buffer is full, the oldest row is evicted
    /// and its tracked object references are released.
    /// </summary>
    /// <param name="cells">The cell data for the row. This array is stored directly (caller must not reuse it).</param>
    /// <param name="originalWidth">The terminal width when this row was captured.</param>
    /// <param name="timestamp">When the row was scrolled off screen.</param>
    /// <returns>The evicted row if the buffer was full; otherwise <c>null</c>.</returns>
    public ScrollbackRow? Push(TerminalCell[] cells, int originalWidth, DateTimeOffset timestamp)
        => PushWithIdentity(cells, originalWidth, timestamp).EvictedRow;

    internal ScrollbackPushResult PushWithIdentity(
        TerminalCell[] cells,
        int originalWidth,
        DateTimeOffset timestamp)
    {
        ScrollbackRow? evicted = null;
        long? evictedRowId = null;

        // Evict oldest row if full
        if (_count == Capacity)
        {
            evicted = _rows[_head];
            evictedRowId = _rowIds[_head];
            ReleaseTrackedObjects(evicted.Value);
        }
        else
        {
            _count++;
        }

        // AddRef tracked objects in the new row
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].TrackedSixel?.AddRef();
            cells[i].TrackedHyperlink?.AddRef();
        }

        var rowId = _nextRowId;
        _nextRowId = checked(_nextRowId + 1);
        _rows[_head] = new ScrollbackRow(cells, originalWidth, timestamp);
        _rowIds[_head] = rowId;
        _head = (_head + 1) % Capacity;

        long? successorRowId = null;
        if (evictedRowId.HasValue)
        {
            successorRowId = _rowIds[_head];
            _rowPruned?.Invoke(new ScrollbackPrunedRow(
                evictedRowId.Value,
                successorRowId,
                ScrollbackPruneReason.Capacity));
        }

        return new ScrollbackPushResult(
            rowId,
            evicted,
            evictedRowId,
            successorRowId);
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> most recent rows, ordered oldest to newest.
    /// </summary>
    public ScrollbackRow[] GetLines(int count)
    {
        var entries = GetEntries(count);
        var result = new ScrollbackRow[entries.Length];
        for (var i = 0; i < entries.Length; i++)
            result[i] = entries[i].Row;
        return result;
    }

    internal ScrollbackEntry[] GetEntries(int count)
    {
        if (count <= 0)
            return [];

        var actual = Math.Min(count, _count);
        var result = new ScrollbackEntry[actual];

        // Start index: oldest of the requested lines
        // _head points to the next write slot. The newest row is at (_head - 1).
        // The oldest row in the buffer is at (_head) when full, or at index 0 when not full.
        var startIndex = _count == Capacity
            ? (_head - actual + Capacity) % Capacity
            : _count - actual;

        for (var i = 0; i < actual; i++)
        {
            var index = (startIndex + i) % Capacity;
            result[i] = new ScrollbackEntry(_rowIds[index], _rows[index]);
        }

        return result;
    }

    /// <summary>
    /// Removes all rows from the buffer, releasing tracked object references.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        int startIndex = _count == Capacity ? _head : 0;

        for (int i = 0; i < _count; i++)
        {
            int idx = (startIndex + i) % Capacity;
            ReleaseTrackedObjects(_rows[idx]);
            var rowId = _rowIds[idx];
            _rows[idx] = default;
            _rowIds[idx] = 0;
            _rowPruned?.Invoke(new ScrollbackPrunedRow(
                rowId,
                SuccessorRowId: null,
                ScrollbackPruneReason.Clear));
        }

        _head = 0;
        _count = 0;
    }

    internal ScrollbackReplacementResult ReplaceRows(
        IReadOnlyList<ReflowScrollbackRow> rows,
        DateTimeOffset timestamp)
    {
        var startIndex = _count == Capacity ? _head : 0;
        for (var i = 0; i < _count; i++)
        {
            var index = (startIndex + i) % Capacity;
            ReleaseTrackedObjects(_rows[index]);
            _rows[index] = default;
            _rowIds[index] = 0;
        }

        _head = 0;
        _count = 0;

        var discarded = Math.Max(0, rows.Count - Capacity);
        for (var rowIndex = discarded; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (var cellIndex = 0; cellIndex < row.Cells.Length; cellIndex++)
            {
                row.Cells[cellIndex].TrackedSixel?.AddRef();
                row.Cells[cellIndex].TrackedHyperlink?.AddRef();
            }

            var id = _nextRowId;
            _nextRowId = checked(_nextRowId + 1);
            _rows[_head] = new ScrollbackRow(
                row.Cells,
                row.OriginalWidth,
                timestamp);
            _rowIds[_head] = id;
            _head = (_head + 1) % Capacity;
            _count++;
        }

        return new ScrollbackReplacementResult(
            GetEntries(_count),
            discarded);
    }

    private static void ReleaseTrackedObjects(ScrollbackRow row)
    {
        if (row.Cells is null)
            return;

        for (int i = 0; i < row.Cells.Length; i++)
        {
            row.Cells[i].TrackedSixel?.Release();
            row.Cells[i].TrackedHyperlink?.Release();
        }
    }
}

/// <summary>
/// A single row stored in the scrollback buffer.
/// </summary>
public readonly record struct ScrollbackRow(
    TerminalCell[] Cells,
    int OriginalWidth,
    DateTimeOffset Timestamp);
