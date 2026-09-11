using Hex1b.Sixel;

namespace Hex1b.Automation;

/// <summary>
/// Decodes Sixel graphics data to raw pixel arrays for SVG rendering.
/// </summary>
/// <remarks>
/// <para>
/// This compatibility shim owns no grammar, color, or raster logic. It consumes
/// the authoritative incremental Sixel parser and delegates every pixel decision
/// to the bounded <c>SixelRasterizer</c>.
/// </para>
/// <para>
/// It returns <see langword="null"/> only for the documented failure and
/// degradation cases: an empty payload, a parse outcome that is not a complete
/// graphic, or an explicit geometry-only raster produced when resource policy
/// refuses pixel allocation.
/// </para>
/// </remarks>
public static class SixelDecoder
{
    /// <summary>
    /// Decodes a Sixel DCS payload to raw RGBA pixel data.
    /// </summary>
    /// <param name="payload">The Sixel payload (including or excluding DCS wrapper).</param>
    /// <param name="cellWidth">The width of a terminal cell in pixels. Unused; retained for source compatibility.</param>
    /// <param name="cellHeight">The height of a terminal cell in pixels. Unused; retained for source compatibility.</param>
    /// <returns>
    /// Decoded image with RGBA pixel data, or <see langword="null"/> when the
    /// payload is empty or the rasterizer explicitly returned geometry only.
    /// </returns>
    public static SixelImage? Decode(string payload, int cellWidth = 9, int cellHeight = 18)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        _ = cellWidth;
        _ = cellHeight;
        return Decode(SixelParser.ParsePayload(payload));
    }

    internal static SixelImage? Decode(SixelParseResult result) =>
        Decode(SixelRasterizer.Rasterize(result, SixelRasterEnvironment.CreateDefault()));

    internal static SixelImage? Decode(SixelData data) => Decode(data.Raster);

    internal static SixelImage? Decode(SixelRasterResult raster)
    {
        if (raster.Status != SixelRasterStatus.Rasterized || raster.Image is null)
        {
            return null;
        }

        var image = raster.Image;
        var pixels = new byte[checked((int)image.PixelCount * 4)];
        var index = 0;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                pixels[index++] = pixel.R;
                pixels[index++] = pixel.G;
                pixels[index++] = pixel.B;
                pixels[index++] = pixel.A;
            }
        }

        return new SixelImage(image.Width, image.Height, pixels);
    }
}

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
