using System.Text;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Re-encodes an already-decoded Sixel pixel buffer back into a self-contained
/// Sixel DCS sequence using an exact (unquantized) color register table, so
/// that re-parsing the result reproduces byte-identical pixels.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Hex1b.Surfaces.SixelEncoder"/> — built for authoring a Sixel
/// payload from an arbitrary RGBA source such as a screenshot, where reducing to a
/// bounded palette is an acceptable, even necessary, lossy step — this encoder
/// exists for the narrower case of round-tripping a buffer that itself came from
/// decoding a valid Sixel raster (a live terminal's placement, on its way to
/// being replayed or recorded). Every color value in such a buffer was produced by
/// <see cref="SixelColorConverter.PercentToComponent"/> (directly, or via
/// <see cref="SixelColorConverter.FromRgbPercent"/>/<see cref="SixelColorConverter.FromHls"/>,
/// or the built-in default palette, all of which route through the same
/// conversion), so inverting that conversion always finds an exact match — no
/// quantization is needed or performed.
/// </para>
/// </remarks>
internal static class SixelExactEncoder
{
    /// <summary>
    /// Encodes <paramref name="buffer"/> into a complete Sixel DCS sequence
    /// (ESC P ... ESC \), matching the shape of <see cref="SixelData.Payload"/>.
    /// </summary>
    /// <returns>
    /// The encoded sequence, or <see langword="null"/> if the buffer's distinct
    /// (exact, unquantized) color count exceeds the number of registers a single
    /// Sixel raster can address (<see cref="SixelEncoder.MaxPaletteColors"/>) —
    /// callers should fall back to the original payload in that case.
    /// </returns>
    internal static string? Encode(SixelPixelBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var width = buffer.Width;
        var height = buffer.Height;

        var palette = new Dictionary<Rgba32, int>();
        var indexedPixels = new int[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = buffer[x, y];
                if (pixel.A == 0)
                {
                    indexedPixels[x, y] = -1;
                    continue;
                }

                if (!palette.TryGetValue(pixel, out var index))
                {
                    if (palette.Count >= SixelEncoder.MaxPaletteColors)
                        return null;

                    index = palette.Count;
                    palette[pixel] = index;
                }

                indexedPixels[x, y] = index;
            }
        }

        if (palette.Count == 0)
            return "\x1bP0;1;0q\x1b\\";

        var sb = new StringBuilder();
        sb.Append("\x1bP0;1;0q");
        sb.Append(FormattableString.Invariant($"\"1;1;{width};{height}"));

        foreach (var (color, index) in palette)
        {
            var r = ComponentToPercent(color.R);
            var g = ComponentToPercent(color.G);
            var b = ComponentToPercent(color.B);
            sb.Append(FormattableString.Invariant($"#{index};2;{r};{g};{b}"));
        }

        var numBands = (height + 5) / 6;
        for (var band = 0; band < numBands; band++)
        {
            var bandStartY = band * 6;
            var bandHeight = Math.Min(6, height - bandStartY);

            foreach (var index in Enumerable.Range(0, palette.Count))
            {
                var run = BuildColorRunForBand(indexedPixels, width, bandStartY, bandHeight, index);
                if (run.Length == 0)
                    continue;

                sb.Append('#');
                sb.Append(index);
                sb.Append(run);
                sb.Append('$');
            }

            if (band < numBands - 1)
                sb.Append('-');
        }

        sb.Append("\x1b\\");
        return sb.ToString();
    }

    private static string BuildColorRunForBand(int[,] indexedPixels, int width, int bandStartY, int bandHeight, int colorIndex)
    {
        var chars = new char[width];
        var lastNonBlank = -1;
        for (var x = 0; x < width; x++)
        {
            byte bits = 0;
            for (var row = 0; row < bandHeight; row++)
            {
                if (indexedPixels[x, bandStartY + row] == colorIndex)
                    bits |= (byte)(1 << row);
            }

            chars[x] = (char)('?' + bits);
            if (bits != 0)
                lastNonBlank = x;
        }

        // This color doesn't appear anywhere in this band — omit its line entirely.
        if (lastNonBlank < 0)
            return string.Empty;

        // Trailing all-blank columns need no representation: nothing follows them
        // on this color's line. Interior blank runs (between two occurrences of
        // this color) MUST be kept — they are what keeps this color's pixels
        // aligned to the correct column when its line is drawn independently of
        // every other color's line.
        var length = lastNonBlank + 1;

        var sb = new StringBuilder();
        var i = 0;
        while (i < length)
        {
            var run = 1;
            while (i + run < length && chars[i + run] == chars[i])
                run++;

            if (run > 3)
            {
                sb.Append('!');
                sb.Append(run);
                sb.Append(chars[i]);
            }
            else
            {
                sb.Append(chars[i], run);
            }

            i += run;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Finds the DEC 0-100 percentage that <see cref="SixelColorConverter.PercentToComponent"/>
    /// converts back to the exact given byte.
    /// </summary>
    /// <remarks>
    /// The scan is at most 101 iterations and runs once per distinct register
    /// color, not per pixel.
    /// </remarks>
    private static int ComponentToPercent(byte component)
    {
        for (var percent = 0; percent <= 100; percent++)
        {
            if (SixelColorConverter.PercentToComponent(percent) == component)
                return percent;
        }

        // Defensive fallback: should be unreachable for genuinely decoded Sixel
        // pixel data. Produces the nearest percentage rather than throwing, since
        // a slightly-off color is preferable to failing the whole re-encode.
        return Math.Clamp(((component * 100) + 127) / 255, 0, 100);
    }
}
