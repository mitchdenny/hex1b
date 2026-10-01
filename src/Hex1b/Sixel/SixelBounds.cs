using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// A pixel-space rectangle.
/// </summary>
/// <param name="X">The left offset, in pixels.</param>
/// <param name="Y">The top offset, in pixels.</param>
/// <param name="Width">The width, in pixels.</param>
/// <param name="Height">The height, in pixels.</param>
public readonly record struct SixelBounds(int X, int Y, int Width, int Height)
{
    /// <summary>An empty (zero-size) bounds.</summary>
    public static SixelBounds Empty { get; } = new(0, 0, 0, 0);

    /// <summary>Gets whether this bounds has zero width or height.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;
}
