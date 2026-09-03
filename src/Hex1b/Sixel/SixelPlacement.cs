using Hex1b.Sixel;

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
/// Everything else about a placement is fixed at creation time, matching the
/// Sixel protocol's write-once-then-anchor semantics: unlike KGP, Sixel has no
/// notion of relocating, resizing, or re-cropping an existing placement.
/// </para>
/// </remarks>
internal sealed class SixelPlacement
{
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
    }

    /// <summary>
    /// The raster resource this placement projects: the authoritative decoded
    /// raster, or a geometry-only outcome when the rasterizer refused pixel
    /// allocation. Also carries this image's logical/rendered/declared/painted
    /// extents, creation-time <see cref="Hex1b.Sixel.SixelCellMetrics"/>,
    /// background mode, aspect state, stable content identity, and protocol
    /// diagnostics (see <see cref="SixelData.ParseResult"/> and
    /// <see cref="SixelData.Raster"/>).
    /// </summary>
    internal SixelData Image { get; }

    /// <summary>
    /// The anchor row (0-based, in the owning screen's local coordinate
    /// space). Mutable so scroll and history operations can shift it in place.
    /// </summary>
    internal int Row { get; set; }

    /// <summary>
    /// The anchor column. Sixel placements never move horizontally, so this
    /// never changes after creation.
    /// </summary>
    internal int Column { get; }

    /// <summary>
    /// The unclipped occupied width, in cells, of the source geometry (the
    /// anchor + occupied cell span the issue requires each placement to
    /// retain, independent of how much of it actually painted).
    /// </summary>
    internal int WidthInCells { get; }

    /// <summary>
    /// The unclipped occupied height, in cells, of the source geometry.
    /// </summary>
    internal int HeightInCells { get; }

    /// <summary>
    /// Row offset (relative to <see cref="Row"/>) where the visible/painted
    /// crop begins. Stored relative to the anchor so shifting <see cref="Row"/>
    /// during scrolling automatically keeps the crop consistent.
    /// </summary>
    internal int PaintedRowOffset { get; }

    /// <summary>
    /// Number of rows actually painted: the visible crop clipped to the
    /// scrolling region/page bounds in effect when the placement was created.
    /// </summary>
    internal int PaintedRowCount { get; }

    /// <summary>
    /// Column offset (relative to <see cref="Column"/>) where the
    /// visible/painted crop begins.
    /// </summary>
    internal int PaintedColumnOffset { get; }

    /// <summary>
    /// Number of columns actually painted.
    /// </summary>
    internal int PaintedColumnCount { get; }

    /// <summary>
    /// Monotonic write sequence used to order overlapping placements (later
    /// sequence paints on top), and to disambiguate otherwise-identical
    /// placements created from the same content.
    /// </summary>
    internal long Sequence { get; }

    /// <summary>When this placement was created.</summary>
    internal DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// <see langword="true"/> when the authoritative rasterizer could not
    /// produce pixels for this placement's image (a geometry-only outcome).
    /// Geometry-only placements are always retained, never silently dropped.
    /// </summary>
    internal bool IsGeometryOnly => Image.Raster.Status == SixelRasterStatus.GeometryOnly;

    /// <summary>
    /// <see langword="true"/> when this placement paints at least one cell.
    /// A graphic clipped entirely outside the scrolling region at creation
    /// time can have a zero-size painted crop while still occupying its
    /// declared cell span.
    /// </summary>
    internal bool HasPaintedExtent => PaintedRowCount > 0 && PaintedColumnCount > 0;

    /// <summary>Absolute top row of the painted/visible crop.</summary>
    internal int PaintedTop => Row + PaintedRowOffset;

    /// <summary>Absolute bottom row (inclusive) of the painted/visible crop.</summary>
    internal int PaintedBottom => PaintedTop + PaintedRowCount - 1;

    /// <summary>Absolute left column of the painted/visible crop.</summary>
    internal int PaintedLeft => Column + PaintedColumnOffset;

    /// <summary>Absolute right column (inclusive) of the painted/visible crop.</summary>
    internal int PaintedRight => PaintedLeft + PaintedColumnCount - 1;

    /// <summary>Whether the painted/visible crop of this placement covers the given cell.</summary>
    internal bool CoversCell(int row, int column) =>
        HasPaintedExtent
        && row >= PaintedTop && row <= PaintedBottom
        && column >= PaintedLeft && column <= PaintedRight;

    /// <summary>Creates a copy of this placement repositioned to <paramref name="row"/>.</summary>
    /// <remarks>
    /// Used when projecting placements into a snapshot: the snapshot's copy is
    /// a fully independent object so it keeps the referenced <see cref="Image"/>
    /// reachable (via ordinary GC) even after the live placement it was copied
    /// from is removed from the active graphics state.
    /// </remarks>
    internal SixelPlacement WithRow(int row) => new(
        Image,
        row,
        Column,
        WidthInCells,
        HeightInCells,
        PaintedRowOffset,
        PaintedRowCount,
        PaintedColumnOffset,
        PaintedColumnCount,
        Sequence,
        CreatedAt);
}
