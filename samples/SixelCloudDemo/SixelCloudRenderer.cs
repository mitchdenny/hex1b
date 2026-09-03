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

    private readonly int _cellPixelWidth;
    private readonly int _cellPixelHeight;

    public SixelCloudRenderer(int cellPixelWidth, int cellPixelHeight)
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
        var width = columns * _cellPixelWidth;

        // The raster stops one row short of the bottom. Painting the last row
        // scrolls the viewport under the default Sixel scrolling mode, which would
        // drag the cloud upward a row every frame.
        var usableRows = Math.Max(1, rows - 1);
        var height = usableRows * _cellPixelHeight;
        var bandCount = (height + SixelBandHeight - 1) / SixelBandHeight;

        // One byte per pixel holding "colour index + 1", so 0 means transparent.
        var canvas = new byte[width * height];
        foreach (var mote in cloud.Motes)
        {
            PaintMote(canvas, mote, width, height);
        }

        var builder = new StringBuilder();

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
        for (var band = 0; band < bandCount; band++)
        {
            AppendBand(builder, canvas, bandMasks, width, height, band);

            if (band < bandCount - 1)
            {
                builder.Append('-');
            }
        }

        builder.Append("\x1b\\");

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
        int width,
        int height,
        int band)
    {
        var bandTop = band * SixelBandHeight;

        // One pass per colour present in this band. A single pass cannot mix
        // colours, so each pass rewinds with DECGCR and overprints its own pixels.
        for (var colorIndex = 0; colorIndex < SixelCloudPalette.Colors.Count; colorIndex++)
        {
            var value = (byte)(colorIndex + 1);
            var present = false;

            for (var x = 0; x < width; x++)
            {
                var mask = 0;
                for (var bit = 0; bit < SixelBandHeight; bit++)
                {
                    var y = bandTop + bit;
                    if (y >= height)
                    {
                        break;
                    }

                    if (canvas[(y * width) + x] == value)
                    {
                        mask |= 1 << bit;
                    }
                }

                bandMasks[x] = mask;
                present |= mask != 0;
            }

            if (!present)
            {
                continue;
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
