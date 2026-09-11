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
    internal enum EncodingOutcome
    {
        Complete,
        PaletteLimitExceeded,
        ByteLimitExceeded,
    }

    internal readonly record struct EncodingResult(EncodingOutcome Outcome, string? Payload);

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
        var result = EncodeBounded(buffer, int.MaxValue, CancellationToken.None);
        return result.Outcome == EncodingOutcome.ByteLimitExceeded ? null : result.Payload;
    }

    internal static EncodingResult EncodeBounded(
        SixelPixelBuffer buffer,
        int maximumBytes,
        CancellationToken cancellationToken,
        bool reuseColorRegisters = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (reuseColorRegisters)
            return EncodeCompositeBounded(buffer, maximumBytes, cancellationToken);

        var width = buffer.Width;
        var height = buffer.Height;

        var palette = new Dictionary<Rgba32, int>();
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var pixel = buffer[x, y];
                if (pixel.A == 0)
                {
                    continue;
                }

                if (!palette.ContainsKey(pixel))
                {
                    if (palette.Count >= SixelEncoder.MaxPaletteColors)
                        return new EncodingResult(EncodingOutcome.PaletteLimitExceeded, null);

                    palette[pixel] = palette.Count;
                }
            }
        }

        if (palette.Count == 0)
        {
            const string empty = "\x1bP0;1;0q\x1b\\";
            return empty.Length <= maximumBytes
                ? new EncodingResult(EncodingOutcome.Complete, empty)
                : new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
        }

        var sb = new StringBuilder();
        if (!TryAppend("\x1bP0;1;0q") ||
            !TryAppend(FormattableString.Invariant($"\"1;1;{width};{height}")))
        {
            return new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
        }

        foreach (var (color, index) in palette)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var r = ComponentToPercent(color.R);
            var g = ComponentToPercent(color.G);
            var b = ComponentToPercent(color.B);
            if (!TryAppend(FormattableString.Invariant($"#{index};2;{r};{g};{b}")))
                return new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
        }
        var colorsByIndex = new Rgba32[palette.Count];
        foreach (var (color, index) in palette)
            colorsByIndex[index] = color;

        var numBands = (height + 5) / 6;
        for (var band = 0; band < numBands; band++)
        {
            var bandStartY = band * 6;
            var bandHeight = Math.Min(6, height - bandStartY);

            foreach (var index in Enumerable.Range(0, palette.Count))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prefix = "#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var remainingBytes = maximumBytes - sb.Length - prefix.Length - 1;
                if (remainingBytes < 0 ||
                    !TryBuildColorRunForBand(
                        buffer,
                        width,
                        bandStartY,
                        bandHeight,
                        colorsByIndex[index],
                        remainingBytes,
                        cancellationToken,
                        out var run))
                {
                    return new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
                }
                if (run.Length == 0)
                    continue;

                if (!TryAppend(prefix) ||
                    !TryAppend(run) ||
                    !TryAppend("$"))
                {
                    return new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
                }
            }

            if (band < numBands - 1 && !TryAppend("-"))
                return new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
        }

        if (!TryAppend("\x1b\\"))
            return new EncodingResult(EncodingOutcome.ByteLimitExceeded, null);
        return new EncodingResult(EncodingOutcome.Complete, sb.ToString());

        bool TryAppend(string value)
        {
            if (value.Length > maximumBytes - sb.Length)
                return false;
            sb.Append(value);
            return true;
        }
    }

    private static EncodingResult EncodeCompositeBounded(
        SixelPixelBuffer buffer, int maximumBytes, CancellationToken cancellationToken)
    {
        // Stream horizontal pixel runs rather than rescanning the whole viewport
        // for every color. Reusing registers cannot recolor already-painted pixels.
        var output = new StringBuilder();
        var palette = new Dictionary<Rgba32, int>();
        var registers = new Rgba32[SixelEncoder.MaxPaletteColors];
        var nextRegister = 0;
        if (!Append(FormattableString.Invariant($"\x1bP0;1;0q\"1;1;{buffer.Width};{buffer.Height}")))
            return new(EncodingOutcome.ByteLimitExceeded, null);
        for (var band = 0; band < buffer.Height; band += 6)
        {
            for (var row = 0; row < 6 && band + row < buffer.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var right = buffer.Width;
                while (right > 0 && buffer[right - 1, band + row].A == 0)
                    right--;
                for (var x = 0; x < right;)
                {
                    var color = buffer[x, band + row];
                    var count = 1;
                    while (x + count < right && buffer[x + count, band + row] == color)
                        count++;
                    var value = '?';
                    if (color.A != 0)
                    {
                        if (!palette.TryGetValue(color, out var index))
                        {
                            index = nextRegister;
                            nextRegister = (nextRegister + 1) % registers.Length;
                            if (palette.Count == registers.Length)
                                palette.Remove(registers[index]);
                            registers[index] = color;
                            palette.Add(color, index);
                            if (!Append(FormattableString.Invariant(
                                $"#{index};2;{ComponentToPercent(color.R)};{ComponentToPercent(color.G)};{ComponentToPercent(color.B)}")))
                                return new(EncodingOutcome.ByteLimitExceeded, null);
                        }
                        else if (!Append("#" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                        {
                            return new(EncodingOutcome.ByteLimitExceeded, null);
                        }
                        value = (char)('?' + (1 << row));
                    }
                    var run = count > 3
                        ? FormattableString.Invariant($"!{count}{value}")
                        : new string(value, count);
                    if (!Append(run))
                        return new(EncodingOutcome.ByteLimitExceeded, null);
                    x += count;
                }
                if (right > 0 && !Append("$"))
                    return new(EncodingOutcome.ByteLimitExceeded, null);
            }
            if (band + 6 < buffer.Height && !Append("-"))
                return new(EncodingOutcome.ByteLimitExceeded, null);
        }
        if (!Append("\x1b\\"))
            return new(EncodingOutcome.ByteLimitExceeded, null);
        return new(EncodingOutcome.Complete, output.ToString());

        bool Append(string text)
        {
            if (text.Length > maximumBytes - output.Length)
                return false;
            output.Append(text);
            return true;
        }
    }

    private static bool TryBuildColorRunForBand(
        SixelPixelBuffer buffer,
        int width,
        int bandStartY,
        int bandHeight,
        Rgba32 color,
        int maximumBytes,
        CancellationToken cancellationToken,
        out string result)
    {
        var sb = new StringBuilder(Math.Min(width, maximumBytes));
        char current = '\0';
        var runLength = 0;
        for (var x = 0; x < width; x++)
        {
            if ((x & 4095) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            byte bits = 0;
            for (var row = 0; row < bandHeight; row++)
            {
                if (buffer[x, bandStartY + row] == color)
                    bits |= (byte)(1 << row);
            }

            var value = (char)('?' + bits);
            if (runLength == 0)
            {
                current = value;
                runLength = 1;
            }
            else if (value == current)
            {
                runLength++;
            }
            else
            {
                if (!TryAppendRun(current, runLength))
                {
                    result = "";
                    return false;
                }
                current = value;
                runLength = 1;
            }
        }

        // Trailing blank columns need no representation. Leading and interior
        // blank runs were emitted when a later non-blank run began.
        if (current != '?' && !TryAppendRun(current, runLength))
        {
            result = "";
            return false;
        }

        result = sb.ToString();
        return true;

        bool TryAppendRun(char value, int length)
        {
            var encodedLength = length > 3
                ? 2 + CountDigits(length)
                : length;
            if (encodedLength > maximumBytes - sb.Length)
                return false;

            if (length > 3)
            {
                sb.Append('!');
                sb.Append(length);
                sb.Append(value);
            }
            else
            {
                sb.Append(value, length);
            }
            return true;
        }
    }

    private static int CountDigits(int value)
    {
        var digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }
        return digits;
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
