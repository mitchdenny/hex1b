namespace Hex1b.Kgp;

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
}
