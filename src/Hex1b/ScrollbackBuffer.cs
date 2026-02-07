namespace Hex1b;

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
    private int _head; // Next write position
    private int _count;

    /// <summary>
    /// Creates a scrollback buffer with the specified maximum line capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of rows to retain.</param>
    public ScrollbackBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _rows = new ScrollbackRow[capacity];
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
    {
        ScrollbackRow? evicted = null;

        // Evict oldest row if full
        if (_count == Capacity)
        {
            evicted = _rows[_head];
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

        _rows[_head] = new ScrollbackRow(cells, originalWidth, timestamp);
        _head = (_head + 1) % Capacity;

        return evicted;
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> most recent rows, ordered oldest to newest.
    /// </summary>
    public ScrollbackRow[] GetLines(int count)
    {
        if (count <= 0)
            return [];

        int actual = Math.Min(count, _count);
        var result = new ScrollbackRow[actual];

        // Start index: oldest of the requested lines
        // _head points to the next write slot. The newest row is at (_head - 1).
        // The oldest row in the buffer is at (_head) when full, or at index 0 when not full.
        int startIndex = _count == Capacity
            ? (_head - actual + Capacity) % Capacity
            : _count - actual;

        for (int i = 0; i < actual; i++)
        {
            result[i] = _rows[(startIndex + i) % Capacity];
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
            _rows[idx] = default;
        }

        _head = 0;
        _count = 0;
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
internal readonly record struct ScrollbackRow(
    TerminalCell[] Cells,
    int OriginalWidth,
    DateTimeOffset Timestamp);
