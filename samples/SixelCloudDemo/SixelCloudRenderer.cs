using System.Text;

/// <summary>
/// The register colours the cloud paints with, as Sixel 0-100 RGB triples.
/// </summary>
/// <remarks>
/// The ramp runs cold to hot: slow motes out in the shallow field are dark blue, and
/// motes whipping through the centre glow red-white. Register 0 is deliberately
/// unused because some terminals treat it as a reserved background slot.
/// </remarks>
internal static class SixelCloudPalette
{
    /// <summary>Cold-to-hot ramp, ordered from slowest to fastest.</summary>
    public static IReadOnlyList<(int Red, int Green, int Blue)> Colors { get; } =
    [
        (8, 10, 34),
        (12, 20, 60),
        (16, 34, 86),
        (24, 55, 100),
        (45, 80, 100),
        (78, 96, 100),
        (100, 92, 72),
        (100, 74, 36),
        (100, 50, 16),
        (100, 26, 12),
        (100, 12, 20),
        (100, 62, 62),
    ];

    /// <summary>The Sixel register number used for the colour at <paramref name="index"/>.</summary>
    public static int RegisterFor(int index) => index + 1;

    /// <summary>
    /// Maps a normalised heat value in [0,1] onto a palette index.
    /// </summary>
    public static int IndexForHeat(double heat)
    {
        var scaled = (int)(heat * Colors.Count);
        return Math.Clamp(scaled, 0, Colors.Count - 1);
    }
}

/// <summary>
/// Turns a frame of <see cref="DustMote"/> values into a raw DCS Sixel sequence.
/// </summary>
/// <remarks>
/// <para>
/// Every sequence here is hand-authored rather than produced by <c>SixelEncoder</c> or
/// <c>SixelWidget</c>. That is deliberate: the demo is only useful as evidence about
/// Hex1b's Sixel handling if the bytes arriving at the terminal were written
/// independently of the code under test.
/// </para>
/// <para>
/// The whole cloud is emitted as a single full-viewport raster rather than as one
/// small placement per mote. Per-mote placements are appealing because they stress
/// placement bookkeeping, but they require cursor positioning before each DCS, and a
/// placement issued near the bottom row scrolls the viewport under the default Sixel
/// scrolling mode, which walked the cloud off the screen. They were also the
/// difference between rendering on WezTerm and rendering almost nothing on iTerm2.
/// A single raster is portable, and it still exercises the interesting path: a large
/// multi-colour image that is fully replaced every frame.
/// </para>
/// </remarks>
internal sealed class SixelCloudRenderer
{
    // Sixel encodes six vertical pixels per character, so band arithmetic is
    // everywhere in this file.
    private const int SixelBandHeight = 6;

    private readonly double _cellPixelWidth;
    private readonly double _cellPixelHeight;

    private byte[]? _canvas;
    private StringBuilder? _builder;

    public SixelCloudRenderer(double cellPixelWidth, double cellPixelHeight)
    {
        _cellPixelWidth = cellPixelWidth;
        _cellPixelHeight = cellPixelHeight;
    }

    /// <summary>
    /// Writes the escape sequences that paint <paramref name="cloud"/> for one frame.
    /// </summary>
    /// <param name="cloud">The simulation state to paint.</param>
    /// <param name="columns">Terminal width in cells.</param>
    /// <param name="rows">Terminal height in cells.</param>
    public byte[] RenderFrame(DustCloud cloud, int columns, int rows)
    {
        // Cell metrics can be fractional when derived from a text-area report, so the
        // raster is rounded down to whole pixels. Rounding down rather than up keeps
        // the image inside the viewport, since overshooting the right edge would clip
        // or wrap.
        var width = Math.Max(1, (int)(columns * _cellPixelWidth));

        // The raster stops one row short of the bottom. Painting the last row
        // scrolls the viewport under the default Sixel scrolling mode, which would
        // drag the cloud upward a row every frame.
        var usableRows = Math.Max(1, rows - 1);
        var height = Math.Max(1, (int)(usableRows * _cellPixelHeight));
        var bandCount = (height + SixelBandHeight - 1) / SixelBandHeight;

        // One byte per pixel holding "colour index + 1", so 0 means transparent.
        // The buffer is reused across frames: at HiDPI metrics this is over ten
        // megabytes, and reallocating it every frame is pure garbage-collector churn.
        var pixelCount = width * height;
        if (_canvas is null || _canvas.Length < pixelCount)
        {
            _canvas = new byte[pixelCount];
        }
        else
        {
            Array.Clear(_canvas, 0, pixelCount);
        }

        var canvas = _canvas;
        foreach (var mote in cloud.Motes)
        {
            PaintMote(canvas, mote, width, height);
        }

        var builder = _builder ??= new StringBuilder();
        builder.Clear();

        // Synchronized output (DEC 2026). The frame erases and then repaints, so
        // without this the terminal is free to present the erased-but-not-yet-painted
        // state, which reads as a flicker or a slideshow rather than animation.
        // Terminals that do not implement 2026 ignore it harmlessly.
        builder.Append("\x1b[?2026h");

        // Home the cursor and erase, so each frame replaces the previous one rather
        // than accumulating. ED has to destroy the previous frame's graphics, not
        // just its text cells, which is the behaviour this demo exists to show.
        builder.Append("\x1b[H\x1b[2J");

        // P1=7 pins square 1:1 pixels instead of the DEC 2:1 default, and P2=1
        // leaves unpainted pixels transparent.
        builder.Append("\x1bP7;1;0q");
        builder.Append("\"1;1;").Append(width).Append(';').Append(height);

        for (var index = 0; index < SixelCloudPalette.Colors.Count; index++)
        {
            var color = SixelCloudPalette.Colors[index];
            builder.Append('#').Append(SixelCloudPalette.RegisterFor(index))
                .Append(";2;")
                .Append(color.Red).Append(';')
                .Append(color.Green).Append(';')
                .Append(color.Blue);
        }

        var bandMasks = new int[width];
        var colorPresent = new bool[SixelCloudPalette.Colors.Count];
        for (var band = 0; band < bandCount; band++)
        {
            AppendBand(builder, canvas, bandMasks, colorPresent, width, height, band);

            if (band < bandCount - 1)
            {
                builder.Append('-');
            }
        }

        builder.Append("\x1b\\");

        // End synchronized output; the terminal now presents the completed frame.
        builder.Append("\x1b[?2026l");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static void PaintMote(byte[] canvas, DustMote mote, int width, int height)
    {
        var pixelX = (int)mote.X;
        var pixelY = (int)mote.Y;
        var value = (byte)(mote.ColorIndex + 1);

        // Each mote is a 2x2 dot so it survives terminal scaling and so motes
        // reliably straddle cell boundaries.
        for (var dy = 0; dy < 2; dy++)
        {
            var y = pixelY + dy;
            if (y < 0 || y >= height)
            {
                continue;
            }

            for (var dx = 0; dx < 2; dx++)
            {
                var x = pixelX + dx;
                if (x < 0 || x >= width)
                {
                    continue;
                }

                canvas[(y * width) + x] = value;
            }
        }
    }

    private static void AppendBand(
        StringBuilder builder,
        byte[] canvas,
        int[] bandMasks,
        bool[] colorPresent,
        int width,
        int height,
        int band)
    {
        var bandTop = band * SixelBandHeight;
        var bandBottom = Math.Min(bandTop + SixelBandHeight, height);

        // One prepass finds which colours this band actually uses. The cloud is very
        // sparse, so a band typically holds a handful of colours out of the full
        // palette; scanning once per palette entry instead would re-read the whole
        // band twelve times over.
        Array.Clear(colorPresent);
        var anyPresent = false;

        for (var y = bandTop; y < bandBottom; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var value = canvas[rowStart + x];
                if (value != 0)
                {
                    colorPresent[value - 1] = true;
                    anyPresent = true;
                }
            }
        }

        if (!anyPresent)
        {
            return;
        }

        // One pass per colour present in this band. A single pass cannot mix
        // colours, so each pass rewinds with DECGCR and overprints its own pixels.
        for (var colorIndex = 0; colorIndex < colorPresent.Length; colorIndex++)
        {
            if (!colorPresent[colorIndex])
            {
                continue;
            }

            var value = (byte)(colorIndex + 1);

            for (var x = 0; x < width; x++)
            {
                var mask = 0;
                for (var y = bandTop; y < bandBottom; y++)
                {
                    if (canvas[(y * width) + x] == value)
                    {
                        mask |= 1 << (y - bandTop);
                    }
                }

                bandMasks[x] = mask;
            }

            builder.Append('#').Append(SixelCloudPalette.RegisterFor(colorIndex));
            AppendRunLengthEncoded(builder, bandMasks, width);

            // DECGCR returns to the left margin so the next colour overprints this
            // band instead of continuing to its right.
            builder.Append('$');
        }
    }

    private static void AppendRunLengthEncoded(StringBuilder builder, int[] bandMasks, int width)
    {
        var runStart = 0;
        while (runStart < width)
        {
            var mask = bandMasks[runStart];
            var runEnd = runStart + 1;
            while (runEnd < width && bandMasks[runEnd] == mask)
            {
                runEnd++;
            }

            // A trailing run of transparent pixels needs no bytes at all: the band
            // just ends early.
            if (mask == 0 && runEnd >= width)
            {
                break;
            }

            var runLength = runEnd - runStart;
            var glyph = (char)('?' + mask);

            // !1 through !3 cost more bytes than simply repeating the character.
            if (runLength > 3)
            {
                builder.Append('!').Append(runLength).Append(glyph);
            }
            else
            {
                builder.Append(glyph, runLength);
            }

            runStart = runEnd;
        }
    }
}
