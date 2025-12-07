namespace Hex1b.Layout;

/// <summary>
/// Represents a 2D size with width and height.
/// </summary>
public readonly struct Size : IEquatable<Size>
{
    public int Width { get; }
    public int Height { get; }

    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public static Size Zero => new(0, 0);

    public bool Equals(Size other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Size other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public override string ToString() => $"Size({Width}, {Height})";

    public static bool operator ==(Size left, Size right) => left.Equals(right);
    public static bool operator !=(Size left, Size right) => !left.Equals(right);
}
