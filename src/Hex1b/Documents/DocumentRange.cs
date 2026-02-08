namespace Hex1b.Documents;

/// <summary>
/// Represents a range in a document defined by start and end offsets.
/// </summary>
public readonly struct DocumentRange : IEquatable<DocumentRange>
{
    public DocumentOffset Start { get; }
    public DocumentOffset End { get; }

    public DocumentRange(DocumentOffset start, DocumentOffset end)
    {
        if (end < start)
            throw new ArgumentException("End must be >= Start.", nameof(end));
        Start = start;
        End = end;
    }

    public int Length => End - Start;
    public bool IsEmpty => Start == End;

    public bool Contains(DocumentOffset offset) => offset >= Start && offset < End;

    public bool Overlaps(DocumentRange other) => !IsEmpty && !other.IsEmpty && Start < other.End && other.Start < End;

    public bool Equals(DocumentRange other) => Start == other.Start && End == other.End;
    public override bool Equals(object? obj) => obj is DocumentRange other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Start, End);
    public override string ToString() => $"[{Start}..{End})";

    public static bool operator ==(DocumentRange left, DocumentRange right) => left.Equals(right);
    public static bool operator !=(DocumentRange left, DocumentRange right) => !left.Equals(right);
}
