using System.Text;

namespace Hex1b.Surfaces;

/// <summary>
/// Represents an RGBA pixel color.
/// </summary>
/// <param name="R">Red component (0-255).</param>
/// <param name="G">Green component (0-255).</param>
/// <param name="B">Blue component (0-255).</param>
/// <param name="A">Alpha component (0-255, 0=transparent, 255=opaque).</param>
public readonly record struct Rgba32(byte R, byte G, byte B, byte A)
{
    /// <summary>
    /// Fully transparent pixel.
    /// </summary>
    public static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    /// Gets whether this pixel is fully transparent (alpha = 0).
    /// </summary>
    public bool IsTransparent => A == 0;

    /// <summary>
    /// Gets whether this pixel is fully opaque (alpha = 255).
    /// </summary>
    public bool IsOpaque => A == 255;

    /// <summary>
    /// Creates an opaque pixel from RGB values.
    /// </summary>
    public static Rgba32 FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);
}
