namespace Hex1b;

/// <summary>
/// Represents a position in the terminal's virtual buffer, which unifies
/// scrollback rows (numbered 0..N-1, oldest to newest) and screen rows
/// (numbered N..N+H-1).
/// </summary>
/// <param name="Row">The virtual row index (0-based, scrollback rows first, then screen rows).</param>
/// <param name="Column">The column index (0-based).</param>
public readonly record struct BufferPosition(int Row, int Column) : IComparable<BufferPosition>
{
    /// <inheritdoc />
    public int CompareTo(BufferPosition other)
    {
        int rowCmp = Row.CompareTo(other.Row);
        return rowCmp != 0 ? rowCmp : Column.CompareTo(other.Column);
    }

    /// <summary>Returns true if this position is before the other position.</summary>
    public bool IsBefore(BufferPosition other) => CompareTo(other) < 0;

    /// <summary>Returns true if this position is after the other position.</summary>
    public bool IsAfter(BufferPosition other) => CompareTo(other) > 0;

    /// <inheritdoc />
    public static bool operator <(BufferPosition left, BufferPosition right) => left.CompareTo(right) < 0;
    /// <inheritdoc />
    public static bool operator >(BufferPosition left, BufferPosition right) => left.CompareTo(right) > 0;
    /// <inheritdoc />
    public static bool operator <=(BufferPosition left, BufferPosition right) => left.CompareTo(right) <= 0;
    /// <inheritdoc />
    public static bool operator >=(BufferPosition left, BufferPosition right) => left.CompareTo(right) >= 0;
}
