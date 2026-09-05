using System.Text;

/// <summary>
/// Turns a frame of <see cref="DustMote"/> values into raw KGP escape sequences.
/// </summary>
/// <remarks>
/// <para>
/// Every sequence here is hand-authored rather than produced by <c>KgpImageWidget</c>,
/// <c>Hex1bRenderContext</c>, or any other part of the library. That is deliberate:
/// the demo is only useful as evidence about Hex1b's KGP handling if the bytes
/// arriving at the terminal were written independently of the code under test.
/// </para>
/// <para>
/// This is the same cloud, on the same physics, as SixelCloudDemo, so the two can be
/// run side by side and compared directly. What differs is the shape of the traffic.
/// Sixel has no notion of a reusable image: a mote's pixels have to be re-encoded and
/// re-sent on every single frame, for every single mote. KGP separates the image from
/// the placement, so the entire palette is transmitted once as a handful of 3x3
/// sprites — a few hundred bytes, sent one time — and after that a frame is nothing
/// but a delete and a few hundred <c>a=p</c> commands that carry no pixel data at all.
/// </para>
/// <para>
/// Three protocol details make the per-frame storm workable, and all three are the
/// point of the demo rather than incidental:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <c>C=1</c> suppresses cursor movement. Without it the cursor advances past every
/// placement, which both costs an extra cursor move per mote and drags the viewport
/// when a placement lands on the last row. It is also why, unlike the Sixel cloud,
/// this renderer can use every row of the terminal.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>q=2</c> suppresses responses. A terminal answers each graphics command with an
/// APC report by default; at several hundred placements per frame that is a flood of
/// input the demo would have to read and discard, and any application that forgets it
/// will appear to hang rather than to animate.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>a=d,d=a</c> clears the previous frame. The lower-case selector deletes visible
/// placements but keeps image data, which is exactly the split this demo depends on:
/// the sprites survive every frame boundary, so they are never retransmitted. The
/// upper-case <c>d=A</c> would free them and turn the demo back into Sixel.
/// </description>
/// </item>
/// </list>
/// <para>
/// Sub-cell motion is expressed with the <c>X</c> and <c>Y</c> keys, which offset the
/// image within its anchor cell. The cursor can only be positioned to a whole cell,
/// so without them every mote would snap to a cell origin and the cloud would read as
/// the grid moving rather than the pixels moving.
/// </para>
/// </remarks>
internal sealed class KgpCloudRenderer : ICloudRenderer
{
    /// <summary>Side of the sprite transmitted for each palette entry, in pixels.</summary>
    private const int MoteSizePixels = 3;

    // Image IDs live in a namespace shared with every other program writing to the
    // same terminal, so the block is placed somewhere an incidental collision is
    // unlikely. It deliberately avoids the very top of the range, which
    // ConsolePresentationAdapter uses for its capability probe.
    private const uint FirstImageId = 7_300u;

    private readonly double _cellPixelWidth;
    private readonly double _cellPixelHeight;
    private readonly int _scaleCells;

    // Largest sub-cell offset the protocol allows, computed once. Truncating rather
    // than rounding keeps the offset strictly inside the cell even when the metrics
    // were derived from a fractional text-area report rather than measured directly.
    private readonly int _maxCellOffsetX;
    private readonly int _maxCellOffsetY;

    // The full palette as transmit commands, built once in the constructor. Together
    // this is the demo's entire pixel budget for the whole run.
    private readonly string _paletteTransmission;

    private bool _paletteTransmitted;

    private StringBuilder? _builder;
    private DustMote[]? _ordered;

    /// <summary>
    /// Creates a renderer for the given cell metrics.
    /// </summary>
    /// <param name="cellPixelWidth">Width of a terminal cell in pixels.</param>
    /// <param name="cellPixelHeight">Height of a terminal cell in pixels.</param>
    /// <param name="scaleCells">
    /// When greater than zero, each mote is scaled to a square of this many cells via
    /// the <c>c</c> and <c>r</c> keys. Zero displays the sprites at their native 3x3.
    /// </param>
    public KgpCloudRenderer(double cellPixelWidth, double cellPixelHeight, int scaleCells = 0)
    {
        _cellPixelWidth = cellPixelWidth;
        _cellPixelHeight = cellPixelHeight;
        _scaleCells = scaleCells;
        _maxCellOffsetX = Math.Max(0, (int)cellPixelWidth - 1);
        _maxCellOffsetY = Math.Max(0, (int)cellPixelHeight - 1);
        _paletteTransmission = BuildPaletteTransmission();
    }

    /// <summary>Total bytes the one-time sprite transmission costs.</summary>
    public int PaletteTransmissionBytes => _paletteTransmission.Length;

    /// <inheritdoc />
    public byte[] RenderFrame(DustCloud cloud, int columns, int rows)
    {
        var builder = _builder ??= new StringBuilder();
        builder.Clear();

        // Synchronized output (DEC 2026). The frame deletes the previous placements
        // and then creates new ones, so without this the terminal is free to present
        // the emptied-but-not-yet-repainted state, which reads as a flicker.
        // Terminals that do not implement 2026 ignore it harmlessly.
        builder.Append("\x1b[?2026h");

        if (!_paletteTransmitted)
        {
            // Sent inside the first frame rather than at startup because the images
            // must land after the workload has switched to the alternate screen: a
            // terminal that keeps separate graphics state per screen buffer would
            // otherwise store them against the normal buffer and lose them.
            _paletteTransmitted = true;
            builder.Append(_paletteTransmission);
        }

        // Clear the previous frame. Lower-case 'a' deletes visible placements and
        // leaves image data alone, so the sprites transmitted above stay resident for
        // the rest of the run. No ED is needed or wanted: the demo never writes text,
        // and erasing cells would not remove graphics anyway.
        builder.Append("\x1b_Ga=d,d=a,q=2\x1b\\");

        // Placements are emitted in cursor order (top-to-bottom, left-to-right) so
        // consecutive motes in the same cell share one cursor move.
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

        Array.Sort(ordered, 0, count, MoteCursorOrder.Instance);

        var cellWidth = _cellPixelWidth;
        var cellHeight = _cellPixelHeight;

        // Unlike the Sixel cloud, the last row is usable. Sixel placements scroll the
        // viewport when they land on the bottom row; a KGP placement issued with C=1
        // moves nothing, and the terminal simply clips whatever falls off the edge.
        var lastRow = -1;
        var lastColumn = -1;
        for (var index = 0; index < count; index++)
        {
            var mote = ordered[index];
            var column = (int)(mote.X / cellWidth);
            var row = (int)(mote.Y / cellHeight);

            if (column < 0 || column >= columns || row < 0 || row >= rows)
            {
                continue;
            }

            // The offsets must stay strictly inside the cell; the protocol gives no
            // meaning to an offset that spills past it, and terminals disagree about
            // what to do with one.
            var offsetX = Math.Clamp((int)(mote.X - (column * cellWidth)), 0, _maxCellOffsetX);
            var offsetY = Math.Clamp((int)(mote.Y - (row * cellHeight)), 0, _maxCellOffsetY);

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
    /// Appends a single mote as a placement of its palette sprite.
    /// </summary>
    /// <remarks>
    /// No placement ID is given, so each command creates a fresh placement rather than
    /// replacing one. That is what makes the frame a single flat batch: naming them
    /// would mean tracking which IDs are still live across frames, and reusing one by
    /// accident would silently drop a mote.
    /// <para>
    /// The z-index is the mote's palette index, so hot motes stack above cold ones
    /// where the cloud overlaps. It costs a handful of bytes and gives the terminal's
    /// occlusion handling something to actually resolve.
    /// </para>
    /// </remarks>
    private void AppendMotePlacement(StringBuilder builder, DustMote mote, int offsetX, int offsetY)
    {
        builder.Append("\x1b_Ga=p,i=").Append(FirstImageId + (uint)mote.ColorIndex);

        if (offsetX > 0)
        {
            builder.Append(",X=").Append(offsetX);
        }

        if (offsetY > 0)
        {
            builder.Append(",Y=").Append(offsetY);
        }

        if (_scaleCells > 0)
        {
            // c and r ask the terminal to scale the sprite into that many cells. The
            // payload does not grow by a byte, which is the part Sixel cannot match:
            // there, a bigger mote is a bigger image on every frame.
            builder.Append(",c=").Append(_scaleCells).Append(",r=").Append(_scaleCells);
        }

        if (mote.ColorIndex > 0)
        {
            builder.Append(",z=").Append(mote.ColorIndex);
        }

        builder.Append(",C=1,q=2\x1b\\");
    }

    /// <summary>
    /// Builds the transmit commands for every palette entry.
    /// </summary>
    /// <remarks>
    /// One 3x3 RGBA sprite per colour. The corners are fully transparent and the edge
    /// pixels are partly so, which turns a nine-pixel square into something that reads
    /// as a round dot and gives the terminal's alpha blending real work at every point
    /// where the cloud overlaps itself.
    /// <para>
    /// Each image is 36 bytes of pixel data, so the payload fits comfortably inside a
    /// single unchunked command and the whole palette costs well under a kilobyte for
    /// the entire run.
    /// </para>
    /// </remarks>
    private static string BuildPaletteTransmission()
    {
        // Alpha by position within the sprite: transparent corners, translucent
        // edges, opaque centre.
        ReadOnlySpan<byte> alpha =
        [
            0, 180, 0,
            180, 255, 180,
            0, 180, 0,
        ];

        var builder = new StringBuilder();
        var pixels = new byte[MoteSizePixels * MoteSizePixels * 4];

        for (var index = 0; index < CloudPalette.Colors.Count; index++)
        {
            var (red, green, blue) = CloudPalette.Colors[index];

            for (var pixel = 0; pixel < alpha.Length; pixel++)
            {
                var offset = pixel * 4;
                pixels[offset] = red;
                pixels[offset + 1] = green;
                pixels[offset + 2] = blue;
                pixels[offset + 3] = alpha[pixel];
            }

            // f=32 is RGBA, t=d sends the pixels inline, and a=t transmits without
            // creating a placement: nothing is displayed until a frame asks for it.
            builder.Append("\x1b_Ga=t,f=32,t=d,s=").Append(MoteSizePixels)
                .Append(",v=").Append(MoteSizePixels)
                .Append(",i=").Append(FirstImageId + (uint)index)
                .Append(",q=2;")
                .Append(Convert.ToBase64String(pixels))
                .Append("\x1b\\");
        }

        return builder.ToString();
    }
}
