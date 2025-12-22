namespace Hex1b.Layout;

/// <summary>
/// Represents a rectangle with position and size.
/// </summary>
public readonly struct Rect : IEquatable<Rect>
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public Rect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public Size Size => new(Width, Height);

    /// <summary>
    /// Checks if a point is inside this rectangle.
    /// </summary>
    /// <param name="px">The X coordinate of the point.</param>
    /// <param name="py">The Y coordinate of the point.</param>
    /// <returns>True if the point is inside the rectangle, false otherwise.</returns>
    public bool Contains(int px, int py) => px >= X && px < Right && py >= Y && py < Bottom;

    public static Rect Zero => new(0, 0, 0, 0);
    public static Rect FromSize(Size size) => new(0, 0, size.Width, size.Height);

    public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"Rect({X}, {Y}, {Width}, {Height})";

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);
}
