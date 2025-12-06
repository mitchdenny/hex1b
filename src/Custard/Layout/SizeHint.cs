namespace Custard.Layout;

/// <summary>
/// Specifies how a widget wants to be sized along one axis.
/// </summary>
public readonly struct SizeHint : IEquatable<SizeHint>
{
    private readonly SizeHintKind _kind;
    private readonly int _value;

    private SizeHint(SizeHintKind kind, int value = 0)
    {
        _kind = kind;
        _value = value;
    }

    /// <summary>
    /// Size to fit content (intrinsic size).
    /// </summary>
    public static SizeHint Content => new(SizeHintKind.Content);

    /// <summary>
    /// Fill available space. Multiple Fill children share space equally.
    /// </summary>
    public static SizeHint Fill => new(SizeHintKind.Fill, 1);

    /// <summary>
    /// Fill available space with a weight. Higher weight = more space.
    /// </summary>
    public static SizeHint Weighted(int weight) => new(SizeHintKind.Fill, weight);

    /// <summary>
    /// Fixed size in characters.
    /// </summary>
    public static SizeHint Fixed(int size) => new(SizeHintKind.Fixed, size);

    /// <summary>
    /// True if this hint requests content-based sizing.
    /// </summary>
    public bool IsContent => _kind == SizeHintKind.Content;

    /// <summary>
    /// True if this hint requests filling available space.
    /// </summary>
    public bool IsFill => _kind == SizeHintKind.Fill;

    /// <summary>
    /// True if this hint specifies a fixed size.
    /// </summary>
    public bool IsFixed => _kind == SizeHintKind.Fixed;

    /// <summary>
    /// The fixed size value (only valid if IsFixed is true).
    /// </summary>
    public int FixedValue => _kind == SizeHintKind.Fixed ? _value : throw new InvalidOperationException("Not a fixed size hint");

    /// <summary>
    /// The fill weight (only valid if IsFill is true).
    /// </summary>
    public int FillWeight => _kind == SizeHintKind.Fill ? _value : throw new InvalidOperationException("Not a fill size hint");

    public bool Equals(SizeHint other) => _kind == other._kind && _value == other._value;
    public override bool Equals(object? obj) => obj is SizeHint other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(_kind, _value);
    public override string ToString() => _kind switch
    {
        SizeHintKind.Content => "Content",
        SizeHintKind.Fill => _value == 1 ? "Fill" : $"Fill({_value})",
        SizeHintKind.Fixed => $"Fixed({_value})",
        _ => "Unknown"
    };

    public static bool operator ==(SizeHint left, SizeHint right) => left.Equals(right);
    public static bool operator !=(SizeHint left, SizeHint right) => !left.Equals(right);

    private enum SizeHintKind
    {
        Content,
        Fill,
        Fixed
    }
}
