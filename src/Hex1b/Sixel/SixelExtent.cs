using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// A width/height pixel extent.
/// </summary>
/// <param name="Width">The width, in pixels.</param>
/// <param name="Height">The height, in pixels.</param>
public readonly record struct SixelExtent(int Width, int Height)
{
    /// <summary>An empty (zero-size) extent.</summary>
    public static SixelExtent Empty { get; } = new(0, 0);
}
