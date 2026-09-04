namespace Hex1b;

/// <summary>
/// A decoded Sixel placement record from a <see cref="Hmp1SixelRecording"/>.
/// </summary>
internal sealed class Hmp1SixelRecordedPlacement(
    int imageIndex,
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
    IReadOnlyList<(int Row, int Column)> damagedCells)
{
    /// <summary>The index of this placement's image within the recording's image table.</summary>
    public int ImageIndex { get; } = imageIndex;

    /// <summary>The anchor row.</summary>
    public int Row { get; } = row;

    /// <summary>The anchor column.</summary>
    public int Column { get; } = column;

    /// <summary>The occupied width in cells.</summary>
    public int WidthInCells { get; } = widthInCells;

    /// <summary>The occupied height in cells.</summary>
    public int HeightInCells { get; } = heightInCells;

    /// <summary>The painted-extent row offset from the anchor.</summary>
    public int PaintedRowOffset { get; } = paintedRowOffset;

    /// <summary>The painted-extent row count.</summary>
    public int PaintedRowCount { get; } = paintedRowCount;

    /// <summary>The painted-extent column offset from the anchor.</summary>
    public int PaintedColumnOffset { get; } = paintedColumnOffset;

    /// <summary>The painted-extent column count.</summary>
    public int PaintedColumnCount { get; } = paintedColumnCount;

    /// <summary>The creation sequence number, used to order overlapping placements.</summary>
    public long Sequence { get; } = sequence;

    /// <summary>The placement's creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; } = createdAt;

    /// <summary>Anchor-relative (row, column) pairs for cells that have been damaged.</summary>
    public IReadOnlyList<(int Row, int Column)> DamagedCells { get; } = damagedCells;
}
