using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b;

/// <summary>
/// An anonymous Sixel raster placement anchored to a terminal cell position.
/// </summary>
/// <remarks>
/// <para>
/// A placement's lifetime is completely independent of the screen buffer's
/// character grid: overwriting the text cell the placement was anchored to
/// does not release the placement or its underlying image. A placement is
/// removed only when it stops being reachable from the graphics state that
/// owns it (the active screen's live placements, or the main screen's
/// history), mirroring the reachability-based lifetime model already used by
/// <see cref="KgpImageStore"/> instead of manual reference counting.
/// </para>
/// <para>
/// <see cref="Row"/> is mutable so scrolling and history transitions can shift
/// a placement's anchor in place without discarding and recreating it (the
/// same "shift, don't recreate" strategy <see cref="KgpPlacement"/> uses).
/// The declared footprint (<see cref="WidthInCells"/>/<see cref="HeightInCells"/>)
/// is fixed at creation time, matching the Sixel protocol's
/// write-once-then-anchor semantics: unlike KGP, Sixel never resizes an
/// existing placement's declared geometry. The painted crop window
/// (<see cref="PaintedRowOffset"/>/<see cref="PaintedRowCount"/>/
/// <see cref="PaintedColumnOffset"/>/<see cref="PaintedColumnCount"/>) may
/// still shrink after creation via <c>ClipToCellRectangle</c> — always
/// by intersecting the *current* painted rectangle with a new clip bound, so
/// a row or column already cropped away can never resurface later regardless
/// of operation order (scroll, resize, and history pruning all funnel through
/// this same monotonic intersection). <see cref="Column"/> is likewise
/// repositioned only via internal reflow machinery, used when an anchor's
/// wrapped-line position genuinely moves horizontally; ordinary scrolling and
/// margin operations never touch it.
/// </para>
/// <para>
/// This type deliberately omits KGP-only protocol concepts: no public
/// image/placement IDs, no image-number addressing, no explicit delete
/// selectors, no relative-placement graph, no Unicode placeholders, no
/// z-index, and no chunked uploads. A placement's identity for automation
/// purposes is its anchor position plus <see cref="Sequence"/> (for
/// disambiguating overlapping placements created from identical content).
/// </para>
/// <para>
/// Whether a placement was captured from the live viewport or from
/// scrollback history is derived, not stored: a placement whose
/// <see cref="Row"/> falls below <see cref="Hex1b.Automation.Hex1bTerminalSnapshot.ScrollbackLineCount"/>
/// is a viewport placement, and one at or above it is a history placement —
/// the same row-space unification <see cref="Hex1b.Automation.Hex1bTerminalSnapshot"/> uses for
/// its text cell buffer.
/// </para>
/// </remarks>
public sealed class SixelPlacement
{
    private readonly HashSet<int> _damagedCells;
    private SixelPixelBuffer? _visiblePixels;

    internal SixelPlacement(
        SixelData image,
        int row,
        int column,
        int widthInCells,
        int heightInCells,
        int paintedRowOffset,
        int paintedRowCount,
        int paintedColumnOffset,
        int paintedColumnCount,
        long sequence,
        DateTimeOffset createdAt)
    {
        Image = image;
        Row = row;
        Column = column;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        PaintedRowOffset = paintedRowOffset;
        PaintedRowCount = paintedRowCount;
        PaintedColumnOffset = paintedColumnOffset;
        PaintedColumnCount = paintedColumnCount;
        Sequence = sequence;
        CreatedAt = createdAt;
        _damagedCells = [];
    }

    private SixelPlacement(
        SixelData image,
        int row,
        int column,
        int widthInCells,
        int heightInCells,
        int paintedRowOffset,
        int paintedRowCount,
        int paintedColumnOffset,
        int paintedColumnCount,
        long sequence,
        DateTimeOffset createdAt,
        HashSet<int> damagedCells)
        : this(
            image,
            row,
            column,
            widthInCells,
            heightInCells,
            paintedRowOffset,
            paintedRowCount,
            paintedColumnOffset,
            paintedColumnCount,
            sequence,
            createdAt)
    {
        _damagedCells = [.. damagedCells];
    }

    /// <summary>
    /// The raster resource this placement projects: the authoritative decoded
    /// raster, or a geometry-only outcome when the rasterizer refused pixel
    /// allocation. Also carries this image's logical/rendered/declared/painted
    /// extents, creation-time <see cref="Hex1b.Sixel.SixelCellMetrics"/>,
    /// background mode, aspect state, stable content identity, and protocol
    /// diagnostics (see <see cref="SixelData.Outcome"/>,
    /// <see cref="SixelData.Diagnostics"/>, <see cref="SixelData.RasterStatus"/>,
    /// and <see cref="SixelData.RasterDiagnostics"/>).
    /// </summary>
    public SixelData Image { get; }

    /// <summary>
    /// The anchor row (0-based, in the owning screen's local coordinate
    /// space). Mutable so scroll and history operations can shift it in place.
    /// </summary>
    public int Row { get; internal set; }

    /// <summary>
    /// The anchor column. Ordinary scrolling and margin operations never
    /// change this; only internal reflow machinery repositions it,
    /// when a wrapped line's anchor genuinely lands in a different column
    /// under the new width.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// The unclipped occupied width, in cells, of the source geometry (the
    /// anchor + occupied cell span the issue requires each placement to
    /// retain, independent of how much of it actually painted).
    /// </summary>
    public int WidthInCells { get; }

    /// <summary>
    /// The unclipped occupied height, in cells, of the source geometry.
    /// </summary>
    public int HeightInCells { get; }

    /// <summary>
    /// Row offset (relative to <see cref="Row"/>) where the visible/painted
    /// crop begins. Stored relative to the anchor so shifting <see cref="Row"/>
    /// during scrolling automatically keeps the crop consistent.
    /// </summary>
    public int PaintedRowOffset { get; }

    /// <summary>
    /// Number of rows actually painted: the visible crop clipped to the
    /// scrolling region/page bounds in effect when the placement was created.
    /// </summary>
    public int PaintedRowCount { get; }

    /// <summary>
    /// Column offset (relative to <see cref="Column"/>) where the
    /// visible/painted crop begins.
    /// </summary>
    public int PaintedColumnOffset { get; }

    /// <summary>
    /// Number of columns actually painted.
    /// </summary>
    public int PaintedColumnCount { get; }

    /// <summary>
    /// Monotonic write sequence used to order overlapping placements (later
    /// sequence paints on top), and to disambiguate otherwise-identical
    /// placements created from the same content.
    /// </summary>
    public long Sequence { get; }

    /// <summary>When this placement was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// <see langword="true"/> when the authoritative rasterizer could not
    /// produce pixels for this placement's image (a geometry-only outcome).
    /// Geometry-only placements are always retained, never silently dropped.
    /// </summary>
    public bool IsGeometryOnly => Image.RasterStatus == SixelRasterStatus.GeometryOnly;

    /// <summary>
    /// <see langword="true"/> when this placement paints at least one cell.
    /// A graphic clipped entirely outside the scrolling region at creation
    /// time can have a zero-size painted crop while still occupying its
    /// declared cell span.
    /// </summary>
    public bool HasPaintedExtent => PaintedRowCount > 0 && PaintedColumnCount > 0;

    /// <summary>Absolute top row of the painted/visible crop.</summary>
    public int PaintedTop => Row + PaintedRowOffset;

    /// <summary>Absolute bottom row (inclusive) of the painted/visible crop.</summary>
    public int PaintedBottom => PaintedTop + PaintedRowCount - 1;

    /// <summary>Absolute left column of the painted/visible crop.</summary>
    public int PaintedLeft => Column + PaintedColumnOffset;

    /// <summary>Absolute right column (inclusive) of the painted/visible crop.</summary>
    public int PaintedRight => PaintedLeft + PaintedColumnCount - 1;

    /// <summary>Whether the painted/visible crop of this placement covers the given cell.</summary>
    public bool CoversCell(int row, int column) =>
        HasPaintedExtent
        && row >= PaintedTop && row <= PaintedBottom
        && column >= PaintedLeft && column <= PaintedRight
        && !IsCellDamaged(row, column);

    /// <summary>
    /// Gets whether this placement still has at least one painted cell that has
    /// not been destructively damaged.
    /// </summary>
    public bool HasVisiblePaintedCells =>
        HasPaintedExtent && _damagedCells.Count < PaintedRowCount * PaintedColumnCount;

    /// <summary>
    /// Destructively clears this placement's pixels that project into the cell.
    /// </summary>
    /// <returns><see langword="true"/> when the cell overlapped a still-visible part of the placement.</returns>
    internal bool DamageCell(int row, int column)
    {
        if (!CoversCell(row, column))
            return false;

        _visiblePixels = null;
        _damagedCells.Add(CellKey(row, column));
        return true;
    }

    /// <summary>
    /// Materializes this placement's pixels with damaged cells made transparent.
    /// </summary>
    public SixelPixelBuffer? GetVisiblePixels()
    {
        var pixels = Image.GetPixels();
        if (pixels is null || _damagedCells.Count == 0)
            return pixels;

        if (_visiblePixels is not null)
            return _visiblePixels;

        var visible = new SixelPixelBuffer(pixels.Width, pixels.Height);
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
                visible[x, y] = pixels[x, y];
        }

        foreach (var key in _damagedCells)
        {
            var relativeRow = key / WidthInCells;
            var relativeColumn = key % WidthInCells;
            var pixelLeft = (int)Math.Floor(relativeColumn * Image.CellMetrics.SafeWidth);
            var pixelRight = (int)Math.Ceiling((relativeColumn + 1) * Image.CellMetrics.SafeWidth);
            var pixelTop = (int)Math.Floor(relativeRow * Image.CellMetrics.SafeHeight);
            var pixelBottom = (int)Math.Ceiling((relativeRow + 1) * Image.CellMetrics.SafeHeight);

            pixelLeft = Math.Clamp(pixelLeft, 0, visible.Width);
            pixelRight = Math.Clamp(pixelRight, 0, visible.Width);
            pixelTop = Math.Clamp(pixelTop, 0, visible.Height);
            pixelBottom = Math.Clamp(pixelBottom, 0, visible.Height);
            for (var y = pixelTop; y < pixelBottom; y++)
            {
                for (var x = pixelLeft; x < pixelRight; x++)
                    visible[x, y] = Rgba32.Transparent;
            }
        }

        _visiblePixels = visible;
        return visible;
    }

    /// <summary>
    /// Materializes exactly the pixels within this placement's painted/visible
    /// crop rectangle (<see cref="PaintedRowOffset"/>/<see cref="PaintedRowCount"/>/
    /// <see cref="PaintedColumnOffset"/>/<see cref="PaintedColumnCount"/>), with
    /// damaged cells made transparent.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="GetVisiblePixels"/> — which always returns the full
    /// declared image, damage-masked but uncropped — this maps the placement's
    /// cell-space crop window into the underlying pixel buffer using the same
    /// <see cref="Hex1b.Sixel.SixelCellMetrics"/>-based rounding
    /// <see cref="GetVisiblePixels"/> uses for damage, so exporters render
    /// exactly what the terminal shows even when scrolling, margin operations,
    /// or history eviction have cropped away part of the originally declared
    /// image. Consumers that need the exact source-crop pixels (SVG/HTML
    /// export, automation assertions) should use this method rather than
    /// re-deriving the crop themselves.
    /// </remarks>
    /// <returns>
    /// The cropped pixel buffer, or <see langword="null"/> when the image has
    /// no decoded raster (geometry-only) or the painted crop is empty.
    /// </returns>
    public SixelPixelBuffer? GetPaintedPixels()
    {
        var full = GetVisiblePixels();
        if (full is null || !HasPaintedExtent)
            return null;

        if (PaintedRowOffset == 0 && PaintedColumnOffset == 0 &&
            PaintedRowCount == HeightInCells && PaintedColumnCount == WidthInCells)
        {
            // Nothing cropped away: avoid an unnecessary copy.
            return full;
        }

        var pixelLeft = Math.Clamp((int)Math.Floor(PaintedColumnOffset * Image.CellMetrics.SafeWidth), 0, full.Width);
        var pixelRight = Math.Clamp((int)Math.Ceiling((PaintedColumnOffset + PaintedColumnCount) * Image.CellMetrics.SafeWidth), 0, full.Width);
        var pixelTop = Math.Clamp((int)Math.Floor(PaintedRowOffset * Image.CellMetrics.SafeHeight), 0, full.Height);
        var pixelBottom = Math.Clamp((int)Math.Ceiling((PaintedRowOffset + PaintedRowCount) * Image.CellMetrics.SafeHeight), 0, full.Height);

        var width = Math.Max(0, pixelRight - pixelLeft);
        var height = Math.Max(0, pixelBottom - pixelTop);
        if (width == 0 || height == 0)
            return null;

        var cropped = new SixelPixelBuffer(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                cropped[x, y] = full[pixelLeft + x, pixelTop + y];
        }

        return cropped;
    }


    /// <summary>Creates a copy of this placement repositioned to <paramref name="row"/>.</summary>
    /// <remarks>
    /// Used when projecting placements into a snapshot: the snapshot's copy is
    /// a fully independent object so it keeps the referenced <see cref="Image"/>
    /// reachable (via ordinary GC) even after the live placement it was copied
    /// from is removed from the active graphics state.
    /// </remarks>
    internal SixelPlacement WithRow(int row) => WithPosition(row, Column);

    /// <summary>
    /// Creates a copy of this placement repositioned to
    /// <paramref name="row"/>/<paramref name="column"/>, used by reflow when
    /// an anchor's wrapped-line position moves both vertically and
    /// horizontally.
    /// </summary>
    /// <remarks>
    /// Damaged-cell keys are anchor-relative (see <see cref="CellKey"/>), so
    /// repositioning the anchor never needs to remap them: the same
    /// image-local (row, column) pair they encode stays correct regardless of
    /// which absolute anchor position <see cref="Row"/>/<see cref="Column"/>
    /// currently hold.
    /// </remarks>
    internal SixelPlacement WithPosition(int row, int column) => new(
        Image,
        row,
        column,
        WidthInCells,
        HeightInCells,
        PaintedRowOffset,
        PaintedRowCount,
        PaintedColumnOffset,
        PaintedColumnCount,
        Sequence,
        CreatedAt,
        _damagedCells);

    /// <summary>
    /// Intersects the current painted rectangle with
    /// <paramref name="top"/>/<paramref name="bottomExclusive"/>/<paramref name="left"/>/<paramref name="rightExclusive"/>,
    /// returning a narrower copy, this same instance when nothing changed, or
    /// <see langword="null"/> when the placement is no longer reachable.
    /// </summary>
    /// <remarks>
    /// This is the single choke point every scroll/resize/history operation
    /// funnels through to shrink a placement's visible window. Because it
    /// always intersects the *current* painted rectangle — never re-derives
    /// from the full declared footprint — a row or column already cropped
    /// away by an earlier operation can never resurface later, regardless of
    /// how many further scrolls or resizes are applied (the "no resurrection"
    /// invariant the issue requires). A geometry-only placement (the
    /// rasterizer refused pixel allocation) is always retained, even with a
    /// zero-size painted window, since its reachability must not depend on
    /// paint status.
    /// </remarks>
    internal SixelPlacement? ClipToCellRectangle(int top, int bottomExclusive, int left, int rightExclusive)
    {
        if (!HasPaintedExtent || top >= bottomExclusive || left >= rightExclusive)
            return IsGeometryOnly ? this : null;

        var clippedTop = Math.Max(PaintedTop, top);
        var clippedBottom = Math.Min(PaintedBottom + 1, bottomExclusive);
        var clippedLeft = Math.Max(PaintedLeft, left);
        var clippedRight = Math.Min(PaintedRight + 1, rightExclusive);

        if (clippedTop >= clippedBottom || clippedLeft >= clippedRight)
        {
            return IsGeometryOnly
                ? new SixelPlacement(
                    Image, Row, Column, WidthInCells, HeightInCells,
                    PaintedRowOffset, 0, PaintedColumnOffset, 0,
                    Sequence, CreatedAt, [])
                : null;
        }

        var newRowOffset = PaintedRowOffset + (clippedTop - PaintedTop);
        var newRowCount = clippedBottom - clippedTop;
        var newColumnOffset = PaintedColumnOffset + (clippedLeft - PaintedLeft);
        var newColumnCount = clippedRight - clippedLeft;

        if (newRowOffset == PaintedRowOffset && newRowCount == PaintedRowCount &&
            newColumnOffset == PaintedColumnOffset && newColumnCount == PaintedColumnCount)
        {
            return this;
        }

        return new SixelPlacement(
            Image, Row, Column, WidthInCells, HeightInCells,
            newRowOffset, newRowCount, newColumnOffset, newColumnCount,
            Sequence, CreatedAt,
            FilterDamagedCells(newRowOffset, newRowCount, newColumnOffset, newColumnCount));
    }

    /// <summary>
    /// Slices a sub-window of the *current* painted rows — <paramref name="retainedRows"/>
    /// rows starting <paramref name="firstRow"/> rows into the current
    /// <see cref="PaintedRowOffset"/> — into a copy whose <see cref="PaintedTop"/>
    /// lands exactly at <paramref name="resultRow"/>. Used both to validate
    /// and to materialize a <see cref="SixelHistoryPlacement"/>'s retained
    /// window.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the requested window is empty or
    /// out of range. Unlike <see cref="ClipToCellRectangle"/>, this never
    /// falls back to retaining a geometry-only placement at zero extent: it
    /// is only ever called with an already-validated, non-empty
    /// <c>FirstRow</c>/<c>RetainedRows</c> pair, so an out-of-range request
    /// here always indicates a genuinely unrepresentable slice.
    /// </remarks>
    internal SixelPlacement? SliceHistoryRows(int firstRow, int retainedRows, int resultRow)
    {
        if (retainedRows <= 0 || firstRow < 0 || firstRow + retainedRows > PaintedRowCount)
            return null;

        // PaintedRowOffset keeps indexing into the image's own rows (damaged-cell
        // keys are image-local), so Row must absorb resultRow's shift on top of
        // that offset — Row + newRowOffset always has to equal resultRow, never
        // resultRow itself, or PaintedTop drifts by whatever the slice skipped.
        var newRowOffset = PaintedRowOffset + firstRow;
        var newRow = resultRow - newRowOffset;
        return new SixelPlacement(
            Image, newRow, Column, WidthInCells, HeightInCells,
            newRowOffset, retainedRows, PaintedColumnOffset, PaintedColumnCount,
            Sequence, CreatedAt,
            FilterDamagedCells(newRowOffset, retainedRows, PaintedColumnOffset, PaintedColumnCount));
    }

    /// <summary>
    /// Filters stale damaged-cell keys that fall outside a narrowed painted
    /// window. Damaged-cell keys are already image-local (anchor-relative),
    /// so no coordinate remapping is needed here — only a range filter, so a
    /// shrinking window never over-counts damage against
    /// <see cref="HasVisiblePaintedCells"/>'s narrower total.
    /// </summary>
    private HashSet<int> FilterDamagedCells(int newRowOffset, int newRowCount, int newColumnOffset, int newColumnCount)
    {
        if (_damagedCells.Count == 0)
            return _damagedCells;

        HashSet<int>? filtered = null;
        foreach (var key in _damagedCells)
        {
            var relativeRow = key / WidthInCells;
            var relativeColumn = key % WidthInCells;
            var stillInWindow =
                relativeRow >= newRowOffset && relativeRow < newRowOffset + newRowCount &&
                relativeColumn >= newColumnOffset && relativeColumn < newColumnOffset + newColumnCount;
            if (stillInWindow)
                continue;

            filtered ??= new HashSet<int>(_damagedCells);
            filtered.Remove(key);
        }

        return filtered ?? _damagedCells;
    }

    /// <summary>
    /// Gets whether text has destructively overwritten this cell's pixels
    /// since the placement was created.
    /// </summary>
    /// <param name="row">The absolute row to check.</param>
    /// <param name="column">The absolute column to check.</param>
    public bool IsCellDamaged(int row, int column) => _damagedCells.Contains(CellKey(row, column));

    private int CellKey(int row, int column) => ((row - Row) * WidthInCells) + (column - Column);
}
