using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node for rendering a table with columns, rows, and optional header/footer.
/// </summary>
/// <typeparam name="TRow">The type of data for each row.</typeparam>
public class TableNode<TRow> : Hex1bNode
{
    // Border characters (single line box drawing)
    private const char TopLeft = '┌';
    private const char TopRight = '┐';
    private const char BottomLeft = '└';
    private const char BottomRight = '┘';
    private const char Horizontal = '─';
    private const char Vertical = '│';
    private const char TeeDown = '┬';
    private const char TeeUp = '┴';
    private const char TeeRight = '├';
    private const char TeeLeft = '┤';
    private const char Cross = '┼';

    /// <summary>
    /// The data source for table rows.
    /// </summary>
    public IReadOnlyList<TRow>? Data { get; set; }

    /// <summary>
    /// Builder for header cells.
    /// </summary>
    public Func<TableHeaderContext, IReadOnlyList<TableCell>>? HeaderBuilder { get; set; }

    /// <summary>
    /// Builder for row cells.
    /// </summary>
    public Func<TableRowContext, TRow, IReadOnlyList<TableCell>>? RowBuilder { get; set; }

    /// <summary>
    /// Builder for footer cells.
    /// </summary>
    public Func<TableFooterContext, IReadOnlyList<TableCell>>? FooterBuilder { get; set; }

    /// <summary>
    /// Builder for empty state widget.
    /// </summary>
    public Func<RootContext, Hex1bWidget>? EmptyBuilder { get; set; }

    /// <summary>
    /// Builder for loading row cells.
    /// </summary>
    public Func<TableLoadingContext, int, IReadOnlyList<TableCell>>? LoadingRowBuilder { get; set; }

    /// <summary>
    /// Number of loading placeholder rows.
    /// </summary>
    public int LoadingRowCount { get; set; } = 3;

    /// <summary>
    /// Currently selected row index.
    /// </summary>
    public int? SelectedIndex { get; set; }

    /// <summary>
    /// Handler for selection changes.
    /// </summary>
    public Func<int, Task>? SelectionChangedHandler { get; set; }

    /// <summary>
    /// Handler for row activation.
    /// </summary>
    public Func<int, TRow, Task>? RowActivatedHandler { get; set; }

    // Computed layout data
    private int _columnCount;
    private int[] _columnWidths = [];
    private IReadOnlyList<TableCell>? _headerCells;
    private List<IReadOnlyList<TableCell>>? _rowCells;
    private IReadOnlyList<TableCell>? _footerCells;

    /// <summary>
    /// Builds cell data from builders and validates column counts.
    /// </summary>
    private void BuildCellData()
    {
        var headerContext = new TableHeaderContext();
        var rowContext = new TableRowContext();
        var footerContext = new TableFooterContext();

        // Build header
        _headerCells = HeaderBuilder?.Invoke(headerContext);
        _columnCount = _headerCells?.Count ?? 0;

        if (_columnCount == 0)
        {
            throw new InvalidOperationException("Table must have at least one column. Header builder returned no cells.");
        }

        // Build rows
        _rowCells = [];
        if (Data is not null && RowBuilder is not null)
        {
            for (int i = 0; i < Data.Count; i++)
            {
                var cells = RowBuilder(rowContext, Data[i]);
                if (cells.Count != _columnCount)
                {
                    throw new InvalidOperationException(
                        $"Table column count mismatch: header has {_columnCount} columns, " +
                        $"but row {i} has {cells.Count} columns.");
                }
                _rowCells.Add(cells);
            }
        }

        // Build footer
        _footerCells = FooterBuilder?.Invoke(footerContext);
        if (_footerCells is not null && _footerCells.Count != _columnCount)
        {
            throw new InvalidOperationException(
                $"Table column count mismatch: header has {_columnCount} columns, " +
                $"but footer has {_footerCells.Count} columns.");
        }
    }

    /// <summary>
    /// Calculates column widths based on content (equal distribution for Phase 1).
    /// </summary>
    private void CalculateColumnWidths(int availableWidth)
    {
        if (_columnCount == 0) return;

        // Account for borders: | col1 | col2 | col3 | = columnCount + 1 vertical bars
        int borderWidth = _columnCount + 1;
        int contentWidth = availableWidth - borderWidth;

        if (contentWidth < _columnCount)
        {
            // Minimum 1 char per column
            contentWidth = _columnCount;
        }

        // Equal distribution for Phase 1
        int baseWidth = contentWidth / _columnCount;
        int remainder = contentWidth % _columnCount;

        _columnWidths = new int[_columnCount];
        for (int i = 0; i < _columnCount; i++)
        {
            _columnWidths[i] = baseWidth + (i < remainder ? 1 : 0);
        }
    }

    public override Size Measure(Constraints constraints)
    {
        BuildCellData();

        // Calculate column widths based on available width
        int width = constraints.MaxWidth > 0 ? constraints.MaxWidth : 80;
        CalculateColumnWidths(width);

        // Calculate height: borders + header + rows + footer
        int height = 2; // Top and bottom borders

        if (_headerCells is not null)
        {
            height += 2; // Header row + separator
        }

        height += _rowCells?.Count ?? 0;

        if (_footerCells is not null)
        {
            height += 2; // Separator + footer row
        }

        // Ensure at least some height for empty/loading states
        if ((_rowCells is null || _rowCells.Count == 0) && Data is not null)
        {
            height += 1; // Space for empty message
        }
        else if (Data is null)
        {
            height += LoadingRowCount; // Loading rows
        }

        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect rect)
    {
        Bounds = rect;
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (_columnCount == 0 || _columnWidths.Length == 0) return;

        int y = 0;
        int totalWidth = _columnWidths.Sum() + _columnCount + 1;

        // Top border
        RenderHorizontalBorder(context, y, TopLeft, TeeDown, TopRight);
        y++;

        // Header
        if (_headerCells is not null)
        {
            RenderRow(context, y, _headerCells, isHeader: true);
            y++;
            RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
            y++;
        }

        // Data rows or loading/empty state
        if (Data is null)
        {
            // Loading state
            var loadingContext = new TableLoadingContext();
            for (int i = 0; i < LoadingRowCount && y < Bounds.Height - 1; i++)
            {
                var loadingCells = LoadingRowBuilder?.Invoke(loadingContext, i);
                if (loadingCells is not null)
                {
                    RenderRow(context, y, loadingCells, isHeader: false);
                }
                else
                {
                    // Default loading placeholder
                    RenderLoadingRow(context, y);
                }
                y++;
            }
        }
        else if (Data.Count == 0)
        {
            // Empty state - for now just show a message
            // TODO: Support custom empty widget
            context.WriteClipped(Bounds.X + 1, Bounds.Y + y, "No data");
            y++;
        }
        else if (_rowCells is not null)
        {
            // Render data rows
            for (int i = 0; i < _rowCells.Count && y < Bounds.Height - (_footerCells is not null ? 3 : 1); i++)
            {
                bool isSelected = SelectedIndex == i;
                RenderRow(context, y, _rowCells[i], isHeader: false, isSelected: isSelected);
                y++;
            }
        }

        // Footer
        if (_footerCells is not null)
        {
            RenderHorizontalBorder(context, y, TeeRight, Cross, TeeLeft);
            y++;
            RenderRow(context, y, _footerCells, isHeader: false, isFooter: true);
            y++;
        }

        // Bottom border
        if (y < Bounds.Height)
        {
            RenderHorizontalBorder(context, y, TopLeft: BottomLeft, middle: TeeUp, right: BottomRight);
        }
    }

    private void RenderHorizontalBorder(Hex1bRenderContext context, int y, char TopLeft, char middle, char right)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(TopLeft);

        for (int col = 0; col < _columnCount; col++)
        {
            sb.Append(new string(Horizontal, _columnWidths[col]));
            if (col < _columnCount - 1)
            {
                sb.Append(middle);
            }
        }

        sb.Append(right);
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderRow(Hex1bRenderContext context, int y, IReadOnlyList<TableCell> cells, 
        bool isHeader, bool isSelected = false, bool isFooter = false)
    {
        var sb = new System.Text.StringBuilder();
        
        // Apply reverse video for selected rows
        if (isSelected)
        {
            sb.Append("\x1b[7m"); // Reverse video on
        }
        
        sb.Append(Vertical);

        for (int col = 0; col < _columnCount && col < cells.Count; col++)
        {
            var cell = cells[col];
            var text = cell.Text ?? "";
            int width = _columnWidths[col];

            // Truncate or pad text to fit column width
            if (text.Length > width)
            {
                text = text[..(width - 1)] + "…";
            }
            else
            {
                text = text.PadRight(width);
            }

            sb.Append(text);

            if (col < _columnCount - 1)
            {
                sb.Append(Vertical);
            }
        }

        sb.Append(Vertical);
        
        if (isSelected)
        {
            sb.Append("\x1b[27m"); // Reverse video off
        }
        
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }

    private void RenderLoadingRow(Hex1bRenderContext context, int y)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Vertical);

        for (int col = 0; col < _columnCount; col++)
        {
            int width = _columnWidths[col];
            sb.Append(new string('░', width));
            if (col < _columnCount - 1)
            {
                sb.Append(Vertical);
            }
        }

        sb.Append(Vertical);
        context.WriteClipped(Bounds.X, Bounds.Y + y, sb.ToString());
    }
}
