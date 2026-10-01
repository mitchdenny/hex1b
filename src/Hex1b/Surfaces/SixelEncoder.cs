using System.Text;

namespace Hex1b.Surfaces;

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
