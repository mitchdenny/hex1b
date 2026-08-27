namespace Hex1b;

/// <summary>
/// Represents a displayed instance of a KGP image at a specific position.
/// </summary>
public sealed class KgpPlacement
{
    /// <summary>The image this placement refers to.</summary>
    public uint ImageId { get; }

    /// <summary>The placement ID (0 if not specified).</summary>
    public uint PlacementId { get; }

    /// <summary>Row where the placement origin is anchored.</summary>
    public int Row { get; internal set; }

    /// <summary>Column where the placement origin is anchored.</summary>
    public int Column { get; }

    /// <summary>Number of columns the placement spans.</summary>
    public uint DisplayColumns { get; }

    /// <summary>Number of rows the placement spans.</summary>
    public uint DisplayRows { get; }

    /// <summary>Source rectangle X offset in pixels.</summary>
    public uint SourceX { get; }

    /// <summary>Source rectangle Y offset in pixels.</summary>
    public uint SourceY { get; }

    /// <summary>Source rectangle width in pixels (0=full).</summary>
    public uint SourceWidth { get; }

    /// <summary>Source rectangle height in pixels (0=full).</summary>
    public uint SourceHeight { get; }

    /// <summary>Z-index for stacking order.</summary>
    public int ZIndex { get; }

    /// <summary>Cell X offset in pixels.</summary>
    public uint CellOffsetX { get; }

    /// <summary>Cell Y offset in pixels.</summary>
    public uint CellOffsetY { get; }

    /// <summary>
    /// Creates a new KGP placement anchored at the specified cell position.
    /// </summary>
    /// <param name="imageId">The image this placement refers to.</param>
    /// <param name="placementId">The placement ID (0 if not specified).</param>
    /// <param name="row">Row where the placement origin is anchored.</param>
    /// <param name="column">Column where the placement origin is anchored.</param>
    /// <param name="displayColumns">Number of columns the placement spans.</param>
    /// <param name="displayRows">Number of rows the placement spans.</param>
    /// <param name="sourceX">Source rectangle X offset in pixels.</param>
    /// <param name="sourceY">Source rectangle Y offset in pixels.</param>
    /// <param name="sourceWidth">Source rectangle width in pixels (0 = full).</param>
    /// <param name="sourceHeight">Source rectangle height in pixels (0 = full).</param>
    /// <param name="zIndex">Z-index for stacking order.</param>
    /// <param name="cellOffsetX">Cell X offset in pixels.</param>
    /// <param name="cellOffsetY">Cell Y offset in pixels.</param>
    public KgpPlacement(
        uint imageId,
        uint placementId,
        int row,
        int column,
        uint displayColumns,
        uint displayRows,
        uint sourceX = 0,
        uint sourceY = 0,
        uint sourceWidth = 0,
        uint sourceHeight = 0,
        int zIndex = 0,
        uint cellOffsetX = 0,
        uint cellOffsetY = 0)
    {
        ImageId = imageId;
        PlacementId = placementId;
        Row = row;
        Column = column;
        DisplayColumns = displayColumns;
        DisplayRows = displayRows;
        SourceX = sourceX;
        SourceY = sourceY;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        ZIndex = zIndex;
        CellOffsetX = cellOffsetX;
        CellOffsetY = cellOffsetY;
    }

    /// <summary>
    /// Whether this placement intersects the given cell position.
    /// </summary>
    public bool IntersectsCell(int row, int column)
    {
        return row >= Row && row < Row + (int)DisplayRows &&
               column >= Column && column < Column + (int)DisplayColumns;
    }

    /// <summary>
    /// Whether this placement intersects the given row.
    /// </summary>
    public bool IntersectsRow(int row) => row >= Row && row < Row + (int)DisplayRows;

    /// <summary>
    /// Whether this placement intersects the given column.
    /// </summary>
    public bool IntersectsColumn(int column) => column >= Column && column < Column + (int)DisplayColumns;

    internal KgpPlacement WithImageId(uint imageId)
        => new(
            imageId,
            PlacementId,
            Row,
            Column,
            DisplayColumns,
            DisplayRows,
            SourceX,
            SourceY,
            SourceWidth,
            SourceHeight,
            ZIndex,
            CellOffsetX,
            CellOffsetY);

    internal KgpPlacement WithPosition(int row, int column)
        => new(
            ImageId,
            PlacementId,
            row,
            column,
            DisplayColumns,
            DisplayRows,
            SourceX,
            SourceY,
            SourceWidth,
            SourceHeight,
            ZIndex,
            CellOffsetX,
            CellOffsetY);

    internal KgpPlacement? ClipToCellRectangle(
        KgpImageData image,
        int top,
        int bottomExclusive,
        int left,
        int rightExclusive,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        if (top >= bottomExclusive || left >= rightExclusive)
            return null;

        var placementTop = (long)Row;
        var placementBottom = placementTop + DisplayRows;
        var placementLeft = (long)Column;
        var placementRight = placementLeft + DisplayColumns;
        var clippedTop = Math.Max(placementTop, top);
        var clippedBottom = Math.Min(placementBottom, bottomExclusive);
        var clippedLeft = Math.Max(placementLeft, left);
        var clippedRight = Math.Min(placementRight, rightExclusive);
        if (clippedTop >= clippedBottom || clippedLeft >= clippedRight)
            return null;

        var firstColumn = checked((uint)(clippedLeft - placementLeft));
        var retainedColumns = checked((uint)(clippedRight - clippedLeft));
        var firstRow = checked((uint)(clippedTop - placementTop));
        var retainedRows = checked((uint)(clippedBottom - clippedTop));
        if (!TryClipSourceAxis(
                image.Width,
                SourceX,
                SourceWidth,
                firstColumn,
                retainedColumns,
                DisplayColumns,
                cellPixelWidth,
                CellOffsetX,
                out var clippedSourceX,
                out var clippedSourceWidth) ||
            !TryClipSourceAxis(
                image.Height,
                SourceY,
                SourceHeight,
                firstRow,
                retainedRows,
                DisplayRows,
                cellPixelHeight,
                CellOffsetY,
                out var clippedSourceY,
                out var clippedSourceHeight))
        {
            return null;
        }

        return new KgpPlacement(
            ImageId,
            PlacementId,
            checked((int)clippedTop),
            checked((int)clippedLeft),
            retainedColumns,
            retainedRows,
            clippedSourceX,
            clippedSourceY,
            clippedSourceWidth,
            clippedSourceHeight,
            ZIndex,
            firstColumn == 0 ? CellOffsetX : 0,
            firstRow == 0 ? CellOffsetY : 0);
    }

    internal KgpPlacement? ClipRows(
        KgpImageData image,
        uint firstRow,
        uint retainedRows,
        int resultRow,
        int cellPixelHeight)
    {
        if (retainedRows == 0 ||
            firstRow >= DisplayRows ||
            retainedRows > DisplayRows - firstRow)
        {
            return null;
        }

        if (!TryNormalizeOrPreserveUnknownSourceAxis(
                image.Width,
                SourceX,
                SourceWidth,
                out var sourceX,
                out var sourceWidth) ||
            !TryClipSourceAxis(
                image.Height,
                SourceY,
                SourceHeight,
                firstRow,
                retainedRows,
                DisplayRows,
                cellPixelHeight,
                CellOffsetY,
                out var clippedSourceY,
                out var clippedSourceHeight))
        {
            return null;
        }

        return new KgpPlacement(
            ImageId,
            PlacementId,
            resultRow,
            Column,
            DisplayColumns,
            retainedRows,
            sourceX,
            clippedSourceY,
            sourceWidth,
            clippedSourceHeight,
            ZIndex,
            CellOffsetX,
            firstRow == 0 ? CellOffsetY : 0);
    }

    internal KgpPlacement Clone()
        => WithImageId(ImageId);

    private static bool TryNormalizeSourceAxis(
        uint imageSize,
        uint sourceOffset,
        uint requestedSize,
        out uint normalizedOffset,
        out uint normalizedSize)
    {
        normalizedOffset = sourceOffset;
        normalizedSize = 0;
        if (imageSize == 0 || sourceOffset >= imageSize)
            return false;

        var available = imageSize - sourceOffset;
        normalizedSize = requestedSize == 0
            ? available
            : Math.Min(requestedSize, available);
        return normalizedSize > 0;
    }

    private static bool TryNormalizeOrPreserveUnknownSourceAxis(
        uint imageSize,
        uint sourceOffset,
        uint requestedSize,
        out uint normalizedOffset,
        out uint normalizedSize)
    {
        if (imageSize == 0)
        {
            normalizedOffset = sourceOffset;
            normalizedSize = requestedSize;
            return true;
        }

        return TryNormalizeSourceAxis(
            imageSize,
            sourceOffset,
            requestedSize,
            out normalizedOffset,
            out normalizedSize);
    }

    private static bool TryClipSourceAxis(
        uint imageSize,
        uint sourceOffset,
        uint requestedSize,
        uint firstCell,
        uint retainedCells,
        uint totalCells,
        int cellPixelSize,
        uint cellOffset,
        out uint clippedOffset,
        out uint clippedSize)
    {
        if (imageSize == 0)
        {
            // PNG dimensions are populated by a future decoding path. Until
            // then source zeroes retain their "unknown/full source" meaning,
            // and clipping affects destination geometry only.
            clippedOffset = sourceOffset;
            clippedSize = requestedSize;
            return true;
        }

        if (!TryNormalizeSourceAxis(
                imageSize,
                sourceOffset,
                requestedSize,
                out var normalizedOffset,
                out var normalizedSize) ||
            !TryProjectSourceAxis(
                normalizedSize,
                firstCell,
                retainedCells,
                totalCells,
                cellPixelSize,
                cellOffset,
                out var projectedOffset,
                out var projectedSize))
        {
            clippedOffset = 0;
            clippedSize = 0;
            return false;
        }

        clippedOffset = checked(normalizedOffset + projectedOffset);
        clippedSize = projectedSize;
        return true;
    }

    private static bool TryProjectSourceAxis(
        uint sourceSize,
        uint firstCell,
        uint retainedCells,
        uint totalCells,
        int cellPixelSize,
        uint cellOffset,
        out uint sourceOffset,
        out uint projectedSize)
    {
        sourceOffset = 0;
        projectedSize = 0;
        if (sourceSize == 0 ||
            totalCells == 0 ||
            retainedCells == 0 ||
            firstCell >= totalCells ||
            retainedCells > totalCells - firstCell)
        {
            return false;
        }

        ulong destinationSize;
        ulong destinationStart;
        ulong destinationEnd;
        if (cellPixelSize > 0)
        {
            var cellSize = (ulong)cellPixelSize;
            var fullSize = checked((ulong)totalCells * cellSize);
            if (cellOffset >= fullSize)
                return false;

            destinationSize = fullSize - cellOffset;
            destinationStart = ProjectCellBoundary(firstCell, cellSize, cellOffset, destinationSize);
            destinationEnd = ProjectCellBoundary(
                checked(firstCell + retainedCells),
                cellSize,
                cellOffset,
                destinationSize);
        }
        else
        {
            // Unknown cell metrics: each destination cell is one proportional
            // unit. Pixel offsets cannot be interpreted without a cell size.
            destinationSize = totalCells;
            destinationStart = firstCell;
            destinationEnd = checked(firstCell + retainedCells);
        }

        if (destinationSize == 0 || destinationStart >= destinationEnd)
            return false;

        var startProduct = checked((ulong)sourceSize * destinationStart);
        var endProduct = checked((ulong)sourceSize * destinationEnd);
        var start = startProduct / destinationSize;
        var end = DivideCeiling(endProduct, destinationSize);
        start = Math.Min(start, sourceSize);
        end = Math.Min(end, sourceSize);
        if (start >= end)
            return false;

        sourceOffset = checked((uint)start);
        projectedSize = checked((uint)(end - start));
        return true;
    }

    private static ulong ProjectCellBoundary(
        uint cell,
        ulong cellPixelSize,
        uint cellOffset,
        ulong destinationSize)
    {
        if (cell == 0)
            return 0;

        var rawBoundary = checked((ulong)cell * cellPixelSize);
        var adjusted = rawBoundary > cellOffset
            ? rawBoundary - cellOffset
            : 0;
        return Math.Min(adjusted, destinationSize);
    }

    private static ulong DivideCeiling(ulong numerator, ulong denominator)
        => numerator / denominator + (numerator % denominator == 0 ? 0UL : 1UL);
}
