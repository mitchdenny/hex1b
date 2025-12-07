namespace Hex1b.Layout;

/// <summary>
/// Represents layout constraints passed from parent to child.
/// The child must return a size that fits within these constraints.
/// </summary>
public readonly struct Constraints : IEquatable<Constraints>
{
    public int MinWidth { get; }
    public int MaxWidth { get; }
    public int MinHeight { get; }
    public int MaxHeight { get; }

    public Constraints(int minWidth, int maxWidth, int minHeight, int maxHeight)
    {
        MinWidth = minWidth;
        MaxWidth = maxWidth;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    /// <summary>
    /// Creates unbounded constraints (infinite max).
    /// </summary>
    public static Constraints Unbounded => new(0, int.MaxValue, 0, int.MaxValue);

    /// <summary>
    /// Creates constraints for a fixed size.
    /// </summary>
    public static Constraints Tight(int width, int height) => new(width, width, height, height);

    /// <summary>
    /// Creates constraints for a fixed size.
    /// </summary>
    public static Constraints Tight(Size size) => Tight(size.Width, size.Height);

    /// <summary>
    /// Creates constraints with max bounds but zero min.
    /// </summary>
    public static Constraints Loose(int maxWidth, int maxHeight) => new(0, maxWidth, 0, maxHeight);

    /// <summary>
    /// Creates constraints with max bounds but zero min.
    /// </summary>
    public static Constraints Loose(Size size) => Loose(size.Width, size.Height);

    /// <summary>
    /// Constrains a size to fit within these constraints.
    /// </summary>
    public Size Constrain(Size size) => new(
        Math.Clamp(size.Width, MinWidth, MaxWidth),
        Math.Clamp(size.Height, MinHeight, MaxHeight)
    );

    /// <summary>
    /// Returns new constraints with the width constrained to a specific value.
    /// </summary>
    public Constraints WithWidth(int width) => new(width, width, MinHeight, MaxHeight);

    /// <summary>
    /// Returns new constraints with the height constrained to a specific value.
    /// </summary>
    public Constraints WithHeight(int height) => new(MinWidth, MaxWidth, height, height);

    /// <summary>
    /// Returns new constraints with the max width reduced.
    /// </summary>
    public Constraints WithMaxWidth(int maxWidth) => new(MinWidth, Math.Min(MaxWidth, maxWidth), MinHeight, MaxHeight);

    /// <summary>
    /// Returns new constraints with the max height reduced.
    /// </summary>
    public Constraints WithMaxHeight(int maxHeight) => new(MinWidth, MaxWidth, MinHeight, Math.Min(MaxHeight, maxHeight));

    public bool Equals(Constraints other) => 
        MinWidth == other.MinWidth && MaxWidth == other.MaxWidth && 
        MinHeight == other.MinHeight && MaxHeight == other.MaxHeight;
    public override bool Equals(object? obj) => obj is Constraints other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(MinWidth, MaxWidth, MinHeight, MaxHeight);
    public override string ToString() => $"Constraints(W:{MinWidth}-{MaxWidth}, H:{MinHeight}-{MaxHeight})";

    public static bool operator ==(Constraints left, Constraints right) => left.Equals(right);
    public static bool operator !=(Constraints left, Constraints right) => !left.Equals(right);
}
