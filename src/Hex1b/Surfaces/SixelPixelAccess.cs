namespace Hex1b.Surfaces;

/// <summary>
/// Provides read-only access to sixel pixel data at a specific cell position.
/// </summary>
/// <remarks>
/// <para>
/// This type is used by computed cells to query pixel data from sixels in layers below.
/// The pixel access is relative to the cell position, allowing effects like tinting
/// or brightness adjustment based on the underlying sixel content.
/// </para>
/// <para>
/// The sixel data is decoded lazily on first access.
/// </para>
/// </remarks>
public readonly struct SixelPixelAccess
{
    private readonly SixelPixelBuffer _buffer;
    private readonly int _offsetX;
    private readonly int _offsetY;
    private readonly CellMetrics _metrics;

    /// <summary>
    /// Gets the width of this cell's pixel region.
    /// </summary>
    public int PixelWidth => _metrics.PixelWidth;

    /// <summary>
    /// Gets the height of this cell's pixel region.
    /// </summary>
    public int PixelHeight => _metrics.PixelHeight;

    /// <summary>
    /// Gets whether this accessor has valid pixel data.
    /// </summary>
    public bool IsValid => _buffer is not null;

    internal SixelPixelAccess(SixelPixelBuffer buffer, int offsetX, int offsetY, CellMetrics metrics)
    {
        _buffer = buffer;
        _offsetX = offsetX;
        _offsetY = offsetY;
        _metrics = metrics;
    }

    /// <summary>
    /// Gets the pixel at the specified position within this cell.
    /// </summary>
    /// <param name="x">X position within the cell (0 to PixelWidth-1).</param>
    /// <param name="y">Y position within the cell (0 to PixelHeight-1).</param>
    /// <returns>The pixel color, or transparent if out of bounds.</returns>
    public Rgba32 GetPixel(int x, int y)
    {
        if (_buffer is null)
            return Rgba32.Transparent;

        var globalX = _offsetX + x;
        var globalY = _offsetY + y;

        return _buffer.GetPixelOrTransparent(globalX, globalY);
    }

    /// <summary>
    /// Gets the average color of all pixels in this cell.
    /// </summary>
    /// <returns>The average color.</returns>
    public Rgba32 GetAverageColor()
    {
        if (_buffer is null)
            return Rgba32.Transparent;

        long r = 0, g = 0, b = 0, a = 0;
        int count = 0;

        for (var y = 0; y < PixelHeight; y++)
        {
            for (var x = 0; x < PixelWidth; x++)
            {
                var pixel = GetPixel(x, y);
                if (pixel.A > 0)
                {
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    a += pixel.A;
                    count++;
                }
            }
        }

        if (count == 0)
            return Rgba32.Transparent;

        return new Rgba32(
            (byte)(r / count),
            (byte)(g / count),
            (byte)(b / count),
            (byte)(a / count));
    }

    /// <summary>
    /// Gets the dominant color of pixels in this cell.
    /// </summary>
    /// <returns>The most common non-transparent color.</returns>
    public Rgba32 GetDominantColor()
    {
        if (_buffer is null)
            return Rgba32.Transparent;

        // Simple approach: quantize colors and count
        var colorCounts = new Dictionary<uint, int>();

        for (var y = 0; y < PixelHeight; y++)
        {
            for (var x = 0; x < PixelWidth; x++)
            {
                var pixel = GetPixel(x, y);
                if (pixel.A < 128)
                    continue;

                // Quantize to reduce unique colors
                var quantized = ((uint)(pixel.R >> 4) << 8) | 
                               ((uint)(pixel.G >> 4) << 4) | 
                               ((uint)(pixel.B >> 4));
                
                colorCounts.TryGetValue(quantized, out var count);
                colorCounts[quantized] = count + 1;
            }
        }

        if (colorCounts.Count == 0)
            return Rgba32.Transparent;

        var dominant = colorCounts.OrderByDescending(kv => kv.Value).First().Key;
        return new Rgba32(
            (byte)(((dominant >> 8) & 0xF) * 17),
            (byte)(((dominant >> 4) & 0xF) * 17),
            (byte)((dominant & 0xF) * 17),
            255);
    }

    /// <summary>
    /// Checks if any pixel in this cell is non-transparent.
    /// </summary>
    public bool HasVisiblePixels()
    {
        if (_buffer is null)
            return false;

        for (var y = 0; y < PixelHeight; y++)
        {
            for (var x = 0; x < PixelWidth; x++)
            {
                if (GetPixel(x, y).A > 0)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Creates a modified copy of the pixel data with a color tint applied.
    /// </summary>
    /// <param name="tintColor">The color to blend with.</param>
    /// <param name="opacity">The blend opacity (0.0 to 1.0).</param>
    /// <returns>A new pixel buffer with the tint applied.</returns>
    public SixelPixelBuffer? WithTint(Rgba32 tintColor, float opacity)
    {
        if (_buffer is null)
            return null;

        opacity = Math.Clamp(opacity, 0f, 1f);
        var result = new SixelPixelBuffer(PixelWidth, PixelHeight);

        for (var y = 0; y < PixelHeight; y++)
        {
            for (var x = 0; x < PixelWidth; x++)
            {
                var pixel = GetPixel(x, y);
                if (pixel.A == 0)
                {
                    result[x, y] = Rgba32.Transparent;
                    continue;
                }

                var r = (byte)(pixel.R * (1 - opacity) + tintColor.R * opacity);
                var g = (byte)(pixel.G * (1 - opacity) + tintColor.G * opacity);
                var b = (byte)(pixel.B * (1 - opacity) + tintColor.B * opacity);

                result[x, y] = new Rgba32(r, g, b, pixel.A);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a modified copy of the pixel data with brightness adjusted.
    /// </summary>
    /// <param name="factor">Brightness factor (1.0 = no change, 0.5 = half brightness, 2.0 = double).</param>
    /// <returns>A new pixel buffer with brightness adjusted.</returns>
    public SixelPixelBuffer? WithBrightness(float factor)
    {
        if (_buffer is null)
            return null;

        var result = new SixelPixelBuffer(PixelWidth, PixelHeight);

        for (var y = 0; y < PixelHeight; y++)
        {
            for (var x = 0; x < PixelWidth; x++)
            {
                var pixel = GetPixel(x, y);
                if (pixel.A == 0)
                {
                    result[x, y] = Rgba32.Transparent;
                    continue;
                }

                var r = (byte)Math.Clamp(pixel.R * factor, 0, 255);
                var g = (byte)Math.Clamp(pixel.G * factor, 0, 255);
                var b = (byte)Math.Clamp(pixel.B * factor, 0, 255);

                result[x, y] = new Rgba32(r, g, b, pixel.A);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a copy of the pixel data for this cell region.
    /// </summary>
    /// <returns>A new pixel buffer containing just this cell's pixels.</returns>
    public SixelPixelBuffer? ToPixelBuffer()
    {
        if (_buffer is null)
            return null;

        var result = new SixelPixelBuffer(PixelWidth, PixelHeight);

        for (var y = 0; y < PixelHeight; y++)
        {
            for (var x = 0; x < PixelWidth; x++)
            {
                result[x, y] = GetPixel(x, y);
            }
        }

        return result;
    }
}
