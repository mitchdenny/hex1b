namespace Hex1b.Documents;

/// <summary>
/// Represents an absolute character offset into a document.
/// </summary>
public readonly struct DocumentOffset : IEquatable<DocumentOffset>, IComparable<DocumentOffset>
{
    public int Value { get; }

    public DocumentOffset(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    public static DocumentOffset Zero => new(0);

    public static implicit operator int(DocumentOffset offset) => offset.Value;
    public static explicit operator DocumentOffset(int value) => new(value);

    public static DocumentOffset operator +(DocumentOffset left, int right) => new(left.Value + right);
    public static DocumentOffset operator -(DocumentOffset left, int right) => new(left.Value - right);
    public static int operator -(DocumentOffset left, DocumentOffset right) => left.Value - right.Value;

    public static bool operator <(DocumentOffset left, DocumentOffset right) => left.Value < right.Value;
    public static bool operator >(DocumentOffset left, DocumentOffset right) => left.Value > right.Value;
    public static bool operator <=(DocumentOffset left, DocumentOffset right) => left.Value <= right.Value;
    public static bool operator >=(DocumentOffset left, DocumentOffset right) => left.Value >= right.Value;

    public int CompareTo(DocumentOffset other) => Value.CompareTo(other.Value);
    public bool Equals(DocumentOffset other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is DocumentOffset other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString();

    public static bool operator ==(DocumentOffset left, DocumentOffset right) => left.Equals(right);
    public static bool operator !=(DocumentOffset left, DocumentOffset right) => !left.Equals(right);
}
