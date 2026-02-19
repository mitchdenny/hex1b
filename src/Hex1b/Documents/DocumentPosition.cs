namespace Hex1b.Documents;

/// <summary>
/// Represents a line and column position in a document. Both are 1-based.
/// </summary>
public readonly struct DocumentPosition : IEquatable<DocumentPosition>, IComparable<DocumentPosition>
{
    public int Line { get; }
    public int Column { get; }

    public DocumentPosition(int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        Line = line;
        Column = column;
    }

    public int CompareTo(DocumentPosition other)
    {
        var lineCmp = Line.CompareTo(other.Line);
        return lineCmp != 0 ? lineCmp : Column.CompareTo(other.Column);
    }

    public bool Equals(DocumentPosition other) => Line == other.Line && Column == other.Column;
    public override bool Equals(object? obj) => obj is DocumentPosition other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Line, Column);
    public override string ToString() => $"({Line},{Column})";

    public static bool operator ==(DocumentPosition left, DocumentPosition right) => left.Equals(right);
    public static bool operator !=(DocumentPosition left, DocumentPosition right) => !left.Equals(right);
    public static bool operator <(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) < 0;
    public static bool operator >(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) > 0;
    public static bool operator <=(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DocumentPosition left, DocumentPosition right) => left.CompareTo(right) >= 0;
}
