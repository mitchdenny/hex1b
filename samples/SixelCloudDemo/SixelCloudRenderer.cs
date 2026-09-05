using System.Text;

/// <summary>
/// The register colours the cloud paints with, as Sixel 0-100 RGB triples.
/// </summary>
/// <remarks>
/// The ramp itself lives in <see cref="CloudPalette"/> at full 8-bit depth, because
/// that is what KGP transmits and both cloud demos are meant to look identical. Sixel
/// colour registers are specified in percent, so each channel is narrowed here.
/// Register 0 is deliberately unused because some terminals treat it as a reserved
/// background slot.
/// </remarks>
internal static class SixelCloudPalette
{
    /// <summary>Cold-to-hot ramp, ordered from slowest to fastest.</summary>
    public static IReadOnlyList<(int Red, int Green, int Blue)> Colors { get; } =
        CloudPalette.Colors.Select(color => (ToPercent(color.Red), ToPercent(color.Green), ToPercent(color.Blue)))
            .ToArray();

    /// <summary>The Sixel register number used for the colour at <paramref name="index"/>.</summary>
    public static int RegisterFor(int index) => index + 1;

    private static int ToPercent(byte channel) => (int)Math.Round(channel * 100.0 / 255.0);
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
/// The default mode emits one small cursor-positioned placement per mote, which is
/// what Sixel is actually for and what stresses placement bookkeeping. A frame is
/// therefore a few hundred tiny DCS strings rather than one enormous one.
/// </para>
/// <para>
/// The alternative full-viewport raster mode is kept behind <c>--raster</c> for
/// comparison. It is far more expensive: at HiDPI cell metrics a full raster is over
/// ten million pixels for the terminal to decode every frame, against roughly seven
/// thousand for the same cloud drawn as per-mote placements. That difference is the
/// difference between smooth animation and a slideshow, so raster mode exists only to
/// demonstrate the contrast.
/// </para>
/// <para>
/// Two hazards make per-mote placements harder than they look, and both are handled
/// here rather than avoided. A placement issued on the last row scrolls the viewport
/// under Sixel scrolling mode, dragging the cloud upward every frame, so placements
/// are clipped to the last usable row rather than relying on DECSDM, whose polarity
/// is inverted between terminals. And motes must be sorted into cursor order, because
/// emitting hundreds of placements that jump the cursor around arbitrarily is what
/// terminals handle worst.
/// </para>
/// </remarks>
internal sealed class SixelCloudRenderer : ICloudRenderer
{
    // Sixel encodes six vertical pixels per character, so band arithmetic is
    // everywhere in this file.
    private const int SixelBandHeight = 6;

    private readonly double _cellPixelWidth;
    private readonly double _cellPixelHeight;
    private readonly bool _useRaster;

    // Every mote placement declares this same raster size. It has to cover a mote
    // anywhere in the cell plus the mote's own 2x2 extent, and the height is rounded
    // up to a whole band because a placement cannot declare a partial one.
    private readonly int _placementWidth;
    private readonly int _placementHeight;

    private byte[]? _canvas;
    private StringBuilder? _builder;
    private DustMote[]? _ordered;

    public SixelCloudRenderer(double cellPixelWidth, double cellPixelHeight, bool useRaster = false)
    {
        _cellPixelWidth = cellPixelWidth;
        _cellPixelHeight = cellPixelHeight;
        _useRaster = useRaster;

        _placementWidth = (int)Math.Ceiling(cellPixelWidth) + 1;

        var coveredHeight = (int)Math.Ceiling(cellPixelHeight) + 1;
        var bands = (coveredHeight + SixelBandHeight - 1) / SixelBandHeight;
        _placementHeight = bands * SixelBandHeight;
    }

    /// <summary>
    /// Writes the escape sequences that paint <paramref name="cloud"/> for one frame.
    /// </summary>
    /// <param name="cloud">The simulation state to paint.</param>
    /// <param name="columns">Terminal width in cells.</param>
    /// <param name="rows">Terminal height in cells.</param>
    public byte[] RenderFrame(DustCloud cloud, int columns, int rows)
        => _useRaster
            ? RenderRasterFrame(cloud, columns, rows)
            : RenderPlacementFrame(cloud, columns, rows);

    /// <summary>
    /// Paints the cloud as one small cursor-positioned placement per mote.
    /// </summary>
    /// <remarks>
    /// This is the mode the demo exists for: a continuous storm of small, independently
    /// positioned placements, which is what a terminal's placement bookkeeping actually
    /// has to cope with.
    /// </remarks>
    private byte[] RenderPlacementFrame(DustCloud cloud, int columns, int rows)
    {
        var builder = _builder ??= new StringBuilder();
        builder.Clear();

        builder.Append("\x1b[?2026h");

        // Deliberately no DECSDM (private mode 80). Its polarity is inverted between
        // terminals: under corrected DEC semantics set disables Sixel scrolling and
        // reset enables it, but xterm implemented the opposite for years. Sending
        // either value would therefore make bottom-row behaviour terminal-dependent.
        // Clipping placements to the last usable row below achieves the same result
        // without depending on which convention the terminal follows.

        builder.Append("\x1b[H\x1b[2J");

        // The last row stays clear even with scrolling disabled, because a placement
        // there still has nowhere to put the six-pixel band it occupies.
        var usableRows = Math.Max(1, rows - 1);

        // Placements are emitted in cursor order (top-to-bottom, left-to-right).
        // Hundreds of placements that jump the cursor around arbitrarily is the access
        // pattern terminals handle worst, and ordering costs almost nothing.
        var motes = cloud.Motes;
        var ordered = _ordered;
        if (ordered is null || ordered.Length < motes.Count)
        {
            ordered = _ordered = new DustMote[motes.Count];
        }

        var count = 0;
        foreach (var mote in motes)
        {
            ordered[count++] = mote;
        }

        var cellWidth = _cellPixelWidth;
        var cellHeight = _cellPixelHeight;
        Array.Sort(ordered, 0, count, MoteCursorOrder.Instance);

        var lastRow = -1;
        var lastColumn = -1;
        for (var index = 0; index < count; index++)
        {
            var mote = ordered[index];
            var column = (int)(mote.X / cellWidth);
            var row = (int)(mote.Y / cellHeight);

            if (column < 0 || column >= columns || row < 0 || row >= usableRows)
            {
                continue;
            }

            // The cursor can only be positioned to a cell, so the sub-cell remainder
            // has to be carried into the placement itself. Dropping it snaps every mote
            // to its cell origin, which reads as the cells moving rather than the
            // pixels moving.
            var offsetX = (int)(mote.X - (column * cellWidth));
            var offsetY = (int)(mote.Y - (row * cellHeight));

            // Skip the cursor move when the previous placement already left the cursor
            // in the right cell, which is common once motes are sorted.
            if (row != lastRow || column != lastColumn)
            {
                builder.Append("\x1b[").Append(row + 1).Append(';').Append(column + 1).Append('H');
                lastRow = row;
                lastColumn = column;
            }

            AppendMotePlacement(builder, mote, offsetX, offsetY);
        }

        builder.Append("\x1b[?2026l");

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Appends a single mote as a small DCS placement offset within its cell.
    /// </summary>
    /// <remarks>
    /// The cursor only positions to a cell boundary, so sub-cell motion has to be
    /// expressed inside the placement. Horizontal offset is leading transparent
    /// columns; vertical offset is leading empty bands plus the bit position within
    /// the band the mote lands in. Without this a mote jumps a whole cell at a time.
    /// <para>
    /// Every placement declares the same raster size regardless of where the mote sits
    /// inside its cell. Sizing the image to the offset instead makes each mote a
    /// differently sized image, and a terminal that rounds or scales images
    /// independently then rounds each one differently, so a drifting mote visibly
    /// changes shape from frame to frame. A constant size costs a few transparent
    /// pixels and keeps every mote identical.
    /// </para>
    /// </remarks>
    private void AppendMotePlacement(StringBuilder builder, DustMote mote, int offsetX, int offsetY)
    {
        var register = SixelCloudPalette.RegisterFor(mote.ColorIndex);
        var color = SixelCloudPalette.Colors[mote.ColorIndex];

        var band = offsetY / SixelBandHeight;
        var bitInBand = offsetY % SixelBandHeight;

        // A 2x2 dot can straddle a band boundary, so the mask is built over two bits
        // and any overflow is carried into the following band.
        var mask = (1 << bitInBand) | (1 << (bitInBand + 1));
        var lowMask = mask & 0x3F;
        var carryMask = (mask >> SixelBandHeight) & 0x3F;

        // Only the one register this mote needs is declared, so each placement stays a
        // few dozen bytes rather than carrying the whole palette.
        builder.Append("\x1bP7;1;0q")
            .Append("\"1;1;")
            .Append(_placementWidth).Append(';')
            .Append(_placementHeight)
            .Append('#').Append(register).Append(";2;")
            .Append(color.Red).Append(';')
            .Append(color.Green).Append(';')
            .Append(color.Blue);

        for (var skipped = 0; skipped < band; skipped++)
        {
            builder.Append('-');
        }

        AppendOffsetRun(builder, register, offsetX, lowMask);

        if (carryMask != 0)
        {
            builder.Append('-');
            AppendOffsetRun(builder, register, offsetX, carryMask);
        }

        builder.Append("\x1b\\");
    }

    /// <summary>
    /// Emits one band: <paramref name="offsetX"/> transparent columns then a two-pixel
    /// run of <paramref name="mask"/>.
    /// </summary>
    private static void AppendOffsetRun(StringBuilder builder, int register, int offsetX, int mask)
    {
        if (offsetX > 0)
        {
            // Transparent padding uses '?' (mask 0) rather than a cursor move, because
            // Sixel has no intra-band horizontal seek.
            builder.Append('!').Append(offsetX).Append('?');
        }

        builder.Append('#').Append(register)
            .Append((char)('?' + mask))
            .Append((char)('?' + mask));
    }

    /// <summary>
    /// Paints the cloud as a single full-viewport raster.
    /// </summary>
    /// <remarks>
    /// Retained behind <c>--raster</c> as the slow comparison case. See the type remarks
    /// for why this is dramatically more expensive for the terminal to decode.
    /// </remarks>
    private byte[] RenderRasterFrame(DustCloud cloud, int columns, int rows)
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
