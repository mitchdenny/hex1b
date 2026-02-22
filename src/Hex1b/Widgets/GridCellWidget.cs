using Hex1b.Layout;

namespace Hex1b.Widgets;

/// <summary>
/// Describes a cell within a <see cref="GridWidget"/>, including its content widget
/// and grid placement (row, column, spans).
/// </summary>
/// <remarks>
/// <para>
/// This is an intermediate builder result, not a standalone <see cref="Hex1bWidget"/>.
/// Use fluent methods to configure placement before returning from the grid builder callback.
/// </para>
/// </remarks>
/// <param name="Child">The content widget rendered inside this cell.</param>
public sealed record GridCellWidget(Hex1bWidget Child)
{
    /// <summary>
    /// The zero-based row index where this cell starts.
    /// </summary>
    internal int RowIndex { get; init; }

    /// <summary>
    /// The number of rows this cell spans. Defaults to 1.
    /// </summary>
    internal int RowSpanCount { get; init; } = 1;

    /// <summary>
    /// The zero-based column index where this cell starts.
    /// </summary>
    internal int ColumnIndex { get; init; }

    /// <summary>
    /// The number of columns this cell spans. Defaults to 1.
    /// </summary>
    internal int ColumnSpanCount { get; init; } = 1;

    /// <summary>
    /// Optional width hint that overrides the column definition for this cell's column.
    /// </summary>
    internal SizeHint? CellWidthHint { get; init; }

    /// <summary>
    /// Optional height hint that overrides the row definition for this cell's row.
    /// </summary>
    internal SizeHint? CellHeightHint { get; init; }

    /// <summary>
    /// Sets the row index for this cell (span of 1).
    /// </summary>
    public GridCellWidget Row(int row) => this with { RowIndex = row, RowSpanCount = 1 };

    /// <summary>
    /// Sets the row index and span count for this cell.
    /// </summary>
    /// <param name="row">The zero-based starting row.</param>
    /// <param name="span">The number of rows to span.</param>
    public GridCellWidget RowSpan(int row, int span) => this with { RowIndex = row, RowSpanCount = span };

    /// <summary>
    /// Sets the column index for this cell (span of 1).
    /// </summary>
    public GridCellWidget Column(int column) => this with { ColumnIndex = column, ColumnSpanCount = 1 };

    /// <summary>
    /// Sets the column index and span count for this cell.
    /// </summary>
    /// <param name="column">The zero-based starting column.</param>
    /// <param name="span">The number of columns to span.</param>
    public GridCellWidget ColumnSpan(int column, int span) => this with { ColumnIndex = column, ColumnSpanCount = span };

    /// <summary>
    /// Sets a fixed width hint for this cell's column.
    /// </summary>
    public GridCellWidget Width(int width) => this with { CellWidthHint = SizeHint.Fixed(width) };

    /// <summary>
    /// Sets this cell's column to fill available width.
    /// </summary>
    public GridCellWidget FillWidth() => this with { CellWidthHint = SizeHint.Fill };

    /// <summary>
    /// Sets this cell's column to fill available width with a weight.
    /// </summary>
    public GridCellWidget FillWidth(int weight) => this with { CellWidthHint = SizeHint.Weighted(weight) };

    /// <summary>
    /// Sets a fixed height hint for this cell's row.
    /// </summary>
    public GridCellWidget Height(int height) => this with { CellHeightHint = SizeHint.Fixed(height) };

    /// <summary>
    /// Sets this cell's row to fill available height.
    /// </summary>
    public GridCellWidget FillHeight() => this with { CellHeightHint = SizeHint.Fill };

    /// <summary>
    /// Sets this cell's row to fill available height with a weight.
    /// </summary>
    public GridCellWidget FillHeight(int weight) => this with { CellHeightHint = SizeHint.Weighted(weight) };
}
