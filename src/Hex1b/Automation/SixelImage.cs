using Hex1b.Sixel;

namespace Hex1b.Automation;

/// <summary>
/// Represents a decoded Sixel image as RGBA pixel data.
/// </summary>
public sealed class SixelImage
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the raw RGBA pixel data (4 bytes per pixel: R, G, B, A).
    /// </summary>
    public byte[] Pixels { get; }

    /// <summary>
    /// Creates a new Sixel image.
    /// </summary>
    public SixelImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}
