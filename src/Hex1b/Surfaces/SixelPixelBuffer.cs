using System.Text;

namespace Hex1b.Surfaces;

/// <summary>
/// Represents a decoded sixel image as RGBA pixels.
/// </summary>
public sealed class SixelPixelBuffer
{
    private readonly Rgba32[] _pixels;

    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Creates a new pixel buffer with the specified dimensions.
    /// All pixels are initialized to transparent.
    /// </summary>
    public SixelPixelBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _pixels = new Rgba32[width * height];
    }

    /// <summary>
    /// Creates a pixel buffer from existing pixel data.
    /// </summary>
    /// <param name="width">Image width.</param>
    /// <param name="height">Image height.</param>
    /// <param name="pixels">Pixel data in row-major order.</param>
    public SixelPixelBuffer(int width, int height, Rgba32[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Length != width * height)
            throw new ArgumentException($"Pixel array length ({pixels.Length}) must match width × height ({width * height})");

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    /// <summary>
    /// Gets or sets the pixel at the specified position.
    /// </summary>
    public Rgba32 this[int x, int y]
    {
        get
        {
            ValidateBounds(x, y);
            return _pixels[y * Width + x];
        }
        set
        {
            ValidateBounds(x, y);
            _pixels[y * Width + x] = value;
        }
    }

    /// <summary>
    /// Gets the pixel at the specified position, or transparent if out of bounds.
    /// </summary>
    public Rgba32 GetPixelOrTransparent(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return Rgba32.Transparent;
        return _pixels[y * Width + x];
    }

    /// <summary>
    /// Gets a span over the pixel data.
    /// </summary>
    public ReadOnlySpan<Rgba32> AsSpan() => _pixels;

    /// <summary>
    /// Gets a span over a single row.
    /// </summary>
    public ReadOnlySpan<Rgba32> GetRow(int y)
    {
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y));
        return _pixels.AsSpan(y * Width, Width);
    }

    /// <summary>
    /// Creates a cropped copy of this buffer.
    /// </summary>
    /// <param name="x">Left edge of crop region.</param>
    /// <param name="y">Top edge of crop region.</param>
    /// <param name="width">Width of crop region.</param>
    /// <param name="height">Height of crop region.</param>
    /// <returns>A new buffer containing the cropped region.</returns>
    public SixelPixelBuffer Crop(int x, int y, int width, int height)
    {
        // Clamp to valid bounds
        var srcX = Math.Max(0, x);
        var srcY = Math.Max(0, y);
        var endX = Math.Min(Width, x + width);
        var endY = Math.Min(Height, y + height);

        var cropWidth = Math.Max(0, endX - srcX);
        var cropHeight = Math.Max(0, endY - srcY);

        if (cropWidth == 0 || cropHeight == 0)
            return new SixelPixelBuffer(1, 1); // Minimum 1x1

        var result = new SixelPixelBuffer(cropWidth, cropHeight);

        for (var row = 0; row < cropHeight; row++)
        {
            var srcRow = _pixels.AsSpan((srcY + row) * Width + srcX, cropWidth);
            var dstRow = result._pixels.AsSpan(row * cropWidth, cropWidth);
            srcRow.CopyTo(dstRow);
        }

        return result;
    }

    /// <summary>
    /// Creates a cropped copy using a PixelRect.
    /// </summary>
    /// <param name="rect">The region to crop.</param>
    /// <returns>A new buffer containing the cropped region.</returns>
    public SixelPixelBuffer Crop(PixelRect rect) => Crop(rect.X, rect.Y, rect.Width, rect.Height);

    internal SixelPixelBuffer Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (width == Width && height == Height)
        {
            return this;
        }

        var resized = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            var sourceY = (int)((long)y * Height / height);
            for (var x = 0; x < width; x++)
            {
                var sourceX = (int)((long)x * Width / width);
                resized[x, y] = this[sourceX, sourceY];
            }
        }

        return resized;
    }

    /// <summary>
    /// Fragments this buffer into multiple cropped buffers based on the specified regions.
    /// </summary>
    /// <param name="regions">The regions to extract (in local coordinates of this buffer).</param>
    /// <returns>
    /// A list of tuples containing the region location and the cropped buffer.
    /// Empty regions are skipped.
    /// </returns>
    public IReadOnlyList<(PixelRect Region, SixelPixelBuffer Buffer)> Fragment(IEnumerable<PixelRect> regions)
    {
        var result = new List<(PixelRect, SixelPixelBuffer)>();

        foreach (var region in regions)
        {
            if (region.IsEmpty)
                continue;

            // Clamp region to buffer bounds
            var bounds = new PixelRect(0, 0, Width, Height);
            var clamped = region.Intersect(bounds);

            if (clamped.IsEmpty)
                continue;

            var cropped = Crop(clamped);
            result.Add((clamped, cropped));
        }

        return result;
    }

    /// <summary>
    /// Computes the visible regions of this buffer after subtracting occluding rectangles.
    /// </summary>
    /// <param name="occlusions">Rectangles that occlude (hide) portions of this buffer.</param>
    /// <returns>List of visible regions that remain after subtracting occlusions.</returns>
    public IReadOnlyList<PixelRect> ComputeVisibleRegions(IEnumerable<PixelRect> occlusions)
    {
        var bounds = new PixelRect(0, 0, Width, Height);
        var visibleRegions = new List<PixelRect> { bounds };

        foreach (var occlusion in occlusions)
        {
            if (occlusion.IsEmpty)
                continue;

            var newRegions = new List<PixelRect>();
            foreach (var region in visibleRegions)
            {
                newRegions.AddRange(region.Subtract(occlusion));
            }
            visibleRegions = newRegions;

            if (visibleRegions.Count == 0)
                break; // Fully occluded
        }

        return visibleRegions;
    }

    private void ValidateBounds(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X must be between 0 and {Width - 1}");
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y must be between 0 and {Height - 1}");
    }
}
