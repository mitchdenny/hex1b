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

/// <summary>
/// Encodes pixel data to Sixel graphics format.
/// </summary>
/// <remarks>
/// <para>
/// Sixel is a bitmap graphics format used by terminals. Each "sixel" character
/// encodes a 1×6 pixel column. This encoder takes RGBA pixel data and produces
/// a sixel payload string that can be embedded in terminal output.
/// </para>
/// <para>
/// The encoder handles:
/// <list type="bullet">
///   <item>Color palette quantization (max 256 colors)</item>
///   <item>Transparency (transparent pixels are not drawn)</item>
///   <item>Run-length encoding for compression</item>
/// </list>
/// </para>
/// </remarks>
public static class SixelEncoder
{
    /// <summary>
    /// Maximum number of colors in the sixel palette.
    /// </summary>
    public const int MaxPaletteColors = 256;

    /// <summary>
    /// Encodes a pixel buffer to a sixel payload string.
    /// </summary>
    /// <param name="buffer">The pixel buffer to encode.</param>
    /// <returns>The sixel-encoded string (DCS ... ST format).</returns>
    public static string Encode(SixelPixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var width = buffer.Width;
        var height = buffer.Height;

        // Build color palette
        var (palette, indexedPixels) = BuildPalette(buffer);

        if (palette.Count == 0)
        {
            // All transparent - return minimal sixel
            return "\x1bP0;1;0q\x1b\\";
        }

        var sb = new StringBuilder();

        // DCS introducer: ESC P 0;1;0 q
        // 0 = pixel aspect ratio (0 = undefined/1:1)
        // 1 = background select (1 = no background change)
        // 0 = horizontal grid size (0 = default)
        sb.Append("\x1bP0;1;0q");

        // Raster attributes: "Pan;Pad;Ph;Pv
        // 1;1 = pixel aspect ratio numerator/denominator
        // Ph;Pv = horizontal/vertical extent in pixels
        sb.Append($"\"1;1;{width};{height}");

        // Color definitions: #Pc;2;Pr;Pg;Pb (RGB 0-100%)
        foreach (var (colorIndex, color) in palette)
        {
            var r = color.R * 100 / 255;
            var g = color.G * 100 / 255;
            var b = color.B * 100 / 255;
            sb.Append($"#{colorIndex};2;{r};{g};{b}");
        }

        // Sixel data organized in bands of 6 rows
        var numBands = (height + 5) / 6;

        for (var band = 0; band < numBands; band++)
        {
            var bandStartY = band * 6;

            // For each color, output all pixels in this band
            foreach (var (colorIndex, _) in palette)
            {
                var colorRun = BuildColorRunForBand(indexedPixels, width, height, bandStartY, colorIndex);

                if (colorRun.Length > 0)
                {
                    sb.Append($"#{colorIndex}");
                    sb.Append(colorRun);
                    sb.Append('$'); // Carriage return within band
                }
            }

            if (band < numBands - 1)
            {
                sb.Append('-'); // Graphics newline (next band)
            }
        }

        // String terminator: ESC \
        sb.Append("\x1b\\");

        return sb.ToString();
    }

    /// <summary>
    /// Encodes raw RGBA byte array to sixel.
    /// </summary>
    /// <param name="rgbaPixels">RGBA pixel data (4 bytes per pixel).</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>The sixel-encoded string.</returns>
    public static string Encode(byte[] rgbaPixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgbaPixels);

        if (rgbaPixels.Length != width * height * 4)
            throw new ArgumentException($"Pixel array length must be width × height × 4 ({width * height * 4}), got {rgbaPixels.Length}");

        var pixels = new Rgba32[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var offset = i * 4;
            pixels[i] = new Rgba32(
                rgbaPixels[offset],
                rgbaPixels[offset + 1],
                rgbaPixels[offset + 2],
                rgbaPixels[offset + 3]);
        }

        return Encode(new SixelPixelBuffer(width, height, pixels));
    }

    #region Private Methods

    private static (Dictionary<int, Rgba32> palette, int[,] indexedPixels) BuildPalette(SixelPixelBuffer buffer)
    {
        var width = buffer.Width;
        var height = buffer.Height;
        var indexedPixels = new int[width, height];

        // Count unique colors (quantized to 6 bits per channel)
        var colorCounts = new Dictionary<Rgba32, int>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = buffer[x, y];

                // Skip transparent pixels
                if (pixel.A < 128)
                {
                    indexedPixels[x, y] = -1;
                    continue;
                }

                // Quantize to 6 bits per channel (64 levels)
                var quantized = new Rgba32(
                    (byte)((pixel.R >> 2) << 2),
                    (byte)((pixel.G >> 2) << 2),
                    (byte)((pixel.B >> 2) << 2),
                    255);

                colorCounts.TryGetValue(quantized, out var count);
                colorCounts[quantized] = count + 1;
            }
        }

        // Take top MaxPaletteColors by frequency
        var topColors = colorCounts
            .OrderByDescending(kv => kv.Value)
            .Take(MaxPaletteColors)
            .Select((kv, index) => (Index: index, Color: kv.Key))
            .ToDictionary(x => x.Index, x => x.Color);

        // Build reverse lookup
        var colorToIndex = topColors.ToDictionary(kv => kv.Value, kv => kv.Key);

        // Map pixels to palette indices
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (indexedPixels[x, y] == -1)
                    continue;

                var pixel = buffer[x, y];
                var quantized = new Rgba32(
                    (byte)((pixel.R >> 2) << 2),
                    (byte)((pixel.G >> 2) << 2),
                    (byte)((pixel.B >> 2) << 2),
                    255);

                if (colorToIndex.TryGetValue(quantized, out var index))
                {
                    indexedPixels[x, y] = index;
                }
                else
                {
                    // Find closest color in palette
                    indexedPixels[x, y] = FindClosestColor(quantized, topColors);
                }
            }
        }

        return (topColors, indexedPixels);
    }

    private static int FindClosestColor(Rgba32 target, Dictionary<int, Rgba32> palette)
    {
        var minDistance = int.MaxValue;
        var closestIndex = 0;

        foreach (var (index, color) in palette)
        {
            var dr = target.R - color.R;
            var dg = target.G - color.G;
            var db = target.B - color.B;
            var distance = dr * dr + dg * dg + db * db;

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = index;
            }
        }

        return closestIndex;
    }

    private static string BuildColorRunForBand(
        int[,] indexedPixels,
        int width,
        int height,
        int bandStartY,
        int colorIndex)
    {
        var sb = new StringBuilder();
        var hasAnyPixels = false;

        var runLength = 0;
        var lastChar = '\0';

        for (var x = 0; x < width; x++)
        {
            // Build sixel value for this column (6 vertical pixels)
            var sixelValue = 0;
            for (var bit = 0; bit < 6; bit++)
            {
                var y = bandStartY + bit;
                if (y < height)
                {
                    var pixelColor = indexedPixels[x, y];
                    if (pixelColor == colorIndex)
                    {
                        sixelValue |= (1 << bit);
                    }
                }
            }

            if (sixelValue != 0)
            {
                hasAnyPixels = true;
            }

            // Sixel character = '?' (63) + 6-bit value
            var sixelChar = (char)(63 + sixelValue);

            // Run-length encoding
            if (sixelChar == lastChar)
            {
                runLength++;
            }
            else
            {
                if (runLength > 0)
                {
                    AppendRun(sb, lastChar, runLength);
                }
                lastChar = sixelChar;
                runLength = 1;
            }
        }

        // Final run
        if (runLength > 0)
        {
            AppendRun(sb, lastChar, runLength);
        }

        return hasAnyPixels ? sb.ToString() : string.Empty;
    }

    private static void AppendRun(StringBuilder sb, char c, int count)
    {
        if (count <= 3)
        {
            // Short runs: repeat character
            sb.Append(c, count);
        }
        else
        {
            // Longer runs: use RLE !count<char>
            sb.Append('!');
            sb.Append(count);
            sb.Append(c);
        }
    }

    #endregion
}
