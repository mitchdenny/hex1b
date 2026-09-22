using System.Text;

namespace Hex1b.Surfaces;

/// <summary>
/// Represents a rectangle in pixel coordinates.
/// </summary>
/// <param name="X">Left edge (inclusive).</param>
/// <param name="Y">Top edge (inclusive).</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Gets the right edge (exclusive).
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Gets the bottom edge (exclusive).
    /// </summary>
    public int Bottom => Y + Height;

    /// <summary>
    /// Gets whether this rectangle has zero area.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Gets the area of this rectangle.
    /// </summary>
    public int Area => Width * Height;

    /// <summary>
    /// Returns the intersection of this rectangle with another.
    /// </summary>
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        if (right <= left || bottom <= top)
            return default;

        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Returns whether this rectangle contains the specified point.
    /// </summary>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>
    /// Returns whether this rectangle fully contains another.
    /// </summary>
    public bool Contains(PixelRect other) =>
        other.X >= X && other.Right <= Right &&
        other.Y >= Y && other.Bottom <= Bottom;

    /// <summary>
    /// Returns whether this rectangle overlaps another.
    /// </summary>
    public bool Overlaps(PixelRect other) => !Intersect(other).IsEmpty;

    /// <summary>
    /// Subtracts a rectangle from this one, returning up to 4 remaining fragments.
    /// </summary>
    /// <param name="hole">The rectangle to subtract.</param>
    /// <returns>List of non-empty fragments remaining after subtraction.</returns>
    public IReadOnlyList<PixelRect> Subtract(PixelRect hole)
    {
        var result = new List<PixelRect>(4);

        // Find actual intersection
        var intersection = Intersect(hole);
        if (intersection.IsEmpty)
        {
            // No overlap - return self
            result.Add(this);
            return result;
        }

        // Top fragment (above the hole)
        if (intersection.Y > Y)
        {
            result.Add(new PixelRect(X, Y, Width, intersection.Y - Y));
        }

        // Bottom fragment (below the hole)
        if (intersection.Bottom < Bottom)
        {
            result.Add(new PixelRect(X, intersection.Bottom, Width, Bottom - intersection.Bottom));
        }

        // Left fragment (left of hole, between top and bottom fragments)
        if (intersection.X > X)
        {
            result.Add(new PixelRect(X, intersection.Y, intersection.X - X, intersection.Height));
        }

        // Right fragment (right of hole, between top and bottom fragments)
        if (intersection.Right < Right)
        {
            result.Add(new PixelRect(intersection.Right, intersection.Y, Right - intersection.Right, intersection.Height));
        }

        return result;
    }
}
