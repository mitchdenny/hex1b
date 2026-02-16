using Hex1b.Reflow;

namespace Hex1b.Reflow;

/// <summary>
/// Shared reflow algorithm used by reflow strategy implementations.
/// </summary>
/// <remarks>
/// The algorithm:
/// <list type="number">
///   <item>Collects all rows (scrollback + screen) into a unified sequence</item>
///   <item>Groups rows into logical lines using the <see cref="CellAttributes.SoftWrap"/> flag</item>
///   <item>Re-wraps each logical line to the new width</item>
///   <item>Distributes re-wrapped rows back into scrollback and screen buffer</item>
/// </list>
/// </remarks>
internal static class ReflowHelper
{
    /// <summary>
    /// Performs terminal content reflow from one width to another.
    /// </summary>
    /// <param name="context">The terminal state before resize.</param>
    /// <param name="preserveCursorRow">
    /// When true, the cursor's row position is anchored — the screen is filled
    /// so the cursor row stays at the same visual position (kitty behavior).
    /// When false, content is filled from the bottom (xterm behavior).
    /// </param>
    /// <returns>The reflowed terminal state.</returns>
    public static ReflowResult PerformReflow(ReflowContext context, bool preserveCursorRow)
    {
        if (context.NewWidth == context.OldWidth && context.NewHeight == context.OldHeight)
        {
            return new ReflowResult(context.ScreenRows, context.ScrollbackRows, context.CursorX, context.CursorY);
        }

        // Step 1: Collect all rows into a unified sequence (scrollback + screen)
        var allRows = CollectAllRows(context);

        // Step 2: Group rows into logical lines using SoftWrap, and track which
        // logical line the cursor row belongs to.
        int scrollbackRowCount = context.ScrollbackRows.Length;
        int cursorAbsoluteRow = scrollbackRowCount + context.CursorY;
        var (logicalLines, cursorLogicalLine, cursorRowInLogicalLine) =
            GroupLogicalLinesWithCursor(allRows, cursorAbsoluteRow);

        // Step 3: Re-wrap all logical lines to the new width
        var rewrappedRows = new List<TerminalCell[]>();
        int newCursorRow = 0;
        int newCursorCol = 0;
        int rowsSoFar = 0;
        bool cursorFound = false;

        for (int lineIdx = 0; lineIdx < logicalLines.Count; lineIdx++)
        {
            var logicalLine = logicalLines[lineIdx];
            var wrappedRows = WrapLogicalLine(logicalLine, context.NewWidth);

            if (!cursorFound && lineIdx == cursorLogicalLine)
            {
                // The cursor is on this logical line. Compute its position within
                // the re-wrapped rows.
                // 
                // The cursor was at (cursorX, some-row) in the old layout.
                // cursorRowInLogicalLine tells us which row of the logical line it was on.
                // Compute the absolute cell offset within this logical line:
                int cellOffsetInLine = cursorRowInLogicalLine * context.OldWidth + context.CursorX;

                // Map to new row/col within the re-wrapped rows
                if (logicalLine.Count == 0)
                {
                    // Empty logical line — cursor goes to column 0
                    newCursorRow = rowsSoFar;
                    newCursorCol = 0;
                }
                else
                {
                    newCursorRow = rowsSoFar + (cellOffsetInLine / context.NewWidth);
                    newCursorCol = cellOffsetInLine % context.NewWidth;
                    
                    // Clamp to the last row of this wrapped line
                    if (newCursorRow >= rowsSoFar + wrappedRows.Count)
                    {
                        newCursorRow = rowsSoFar + wrappedRows.Count - 1;
                        newCursorCol = Math.Min(newCursorCol, context.NewWidth - 1);
                    }
                }
                cursorFound = true;
            }

            rewrappedRows.AddRange(wrappedRows);
            rowsSoFar += wrappedRows.Count;
        }

        if (!cursorFound)
        {
            newCursorRow = Math.Max(0, rewrappedRows.Count - 1);
            newCursorCol = 0;
        }

        // Step 4: Distribute rows into scrollback and screen
        return DistributeRows(rewrappedRows, context, newCursorRow, newCursorCol, preserveCursorRow);
    }

    /// <summary>
    /// Collects all rows (scrollback + screen) into a single list of cell arrays.
    /// </summary>
    private static List<TerminalCell[]> CollectAllRows(ReflowContext context)
    {
        var allRows = new List<TerminalCell[]>(context.ScrollbackRows.Length + context.ScreenRows.Length);

        // Add scrollback rows (may have different widths — normalize to OldWidth if needed)
        foreach (var sbRow in context.ScrollbackRows)
        {
            if (sbRow.Cells.Length == context.OldWidth)
            {
                allRows.Add(sbRow.Cells);
            }
            else
            {
                // Normalize to current width for consistent processing
                var normalized = new TerminalCell[context.OldWidth];
                int copyLen = Math.Min(sbRow.Cells.Length, context.OldWidth);
                Array.Copy(sbRow.Cells, normalized, copyLen);
                for (int x = copyLen; x < context.OldWidth; x++)
                    normalized[x] = TerminalCell.Empty;
                allRows.Add(normalized);
            }
        }

        // Add screen rows
        foreach (var screenRow in context.ScreenRows)
        {
            allRows.Add(screenRow);
        }

        return allRows;
    }

    /// <summary>
    /// Groups rows into logical lines and tracks which logical line the cursor row belongs to.
    /// A logical line is a sequence of rows where each row (except the last) has
    /// <see cref="CellAttributes.SoftWrap"/> set on its last cell.
    /// </summary>
    /// <returns>
    /// A tuple of (logicalLines, cursorLogicalLineIndex, cursorRowWithinLogicalLine).
    /// cursorRowWithinLogicalLine is the 0-based index of the cursor's row within its logical line.
    /// </returns>
    private static (List<List<TerminalCell>> logicalLines, int cursorLogicalLine, int cursorRowInLine)
        GroupLogicalLinesWithCursor(List<TerminalCell[]> allRows, int cursorAbsoluteRow)
    {
        var logicalLines = new List<List<TerminalCell>>();
        var currentLine = new List<TerminalCell>();
        int currentLineStartRow = 0;
        int cursorLogicalLine = -1;
        int cursorRowInLine = 0;

        for (int rowIdx = 0; rowIdx < allRows.Count; rowIdx++)
        {
            var row = allRows[rowIdx];
            int lastNonEmpty = row.Length - 1;
            bool hasSoftWrap = row.Length > 0 && (row[^1].Attributes & CellAttributes.SoftWrap) != 0;

            if (hasSoftWrap)
            {
                // Row is soft-wrapped: add all cells (including trailing spaces)
                for (int x = 0; x < row.Length; x++)
                {
                    var cell = row[x];
                    if (x == row.Length - 1)
                        cell = cell with { Attributes = cell.Attributes & ~CellAttributes.SoftWrap };
                    currentLine.Add(cell);
                }
            }
            else
            {
                // Row ends with a hard break: trim trailing empty cells
                while (lastNonEmpty >= 0 && IsEmptyCell(row[lastNonEmpty]))
                    lastNonEmpty--;

                for (int x = 0; x <= lastNonEmpty; x++)
                    currentLine.Add(row[x]);

                // Track cursor before finalizing the logical line
                if (cursorLogicalLine < 0 && cursorAbsoluteRow >= currentLineStartRow && cursorAbsoluteRow <= rowIdx)
                {
                    cursorLogicalLine = logicalLines.Count;
                    cursorRowInLine = cursorAbsoluteRow - currentLineStartRow;
                }

                logicalLines.Add(currentLine);
                currentLine = new List<TerminalCell>();
                currentLineStartRow = rowIdx + 1;
            }
        }

        // Don't forget the last line if it ended with a soft-wrap
        if (currentLine.Count > 0)
        {
            if (cursorLogicalLine < 0 && cursorAbsoluteRow >= currentLineStartRow)
            {
                cursorLogicalLine = logicalLines.Count;
                cursorRowInLine = cursorAbsoluteRow - currentLineStartRow;
            }
            logicalLines.Add(currentLine);
        }

        // Ensure at least one logical line exists
        if (logicalLines.Count == 0)
            logicalLines.Add(new List<TerminalCell>());

        // If cursor wasn't found (shouldn't happen), default to last line
        if (cursorLogicalLine < 0)
        {
            cursorLogicalLine = logicalLines.Count - 1;
            cursorRowInLine = 0;
        }

        return (logicalLines, cursorLogicalLine, cursorRowInLine);
    }

    /// <summary>
    /// Wraps a logical line of cells to the specified width, producing one or more rows.
    /// </summary>
    private static List<TerminalCell[]> WrapLogicalLine(List<TerminalCell> cells, int newWidth)
    {
        var rows = new List<TerminalCell[]>();

        if (cells.Count == 0)
        {
            // Empty logical line → one empty row
            var emptyRow = new TerminalCell[newWidth];
            Array.Fill(emptyRow, TerminalCell.Empty);
            rows.Add(emptyRow);
            return rows;
        }

        int cellIndex = 0;
        while (cellIndex < cells.Count)
        {
            var row = new TerminalCell[newWidth];
            int col = 0;

            while (col < newWidth && cellIndex < cells.Count)
            {
                var cell = cells[cellIndex];
                var graphemeWidth = GetCellDisplayWidth(cell);

                // Wide character that doesn't fit at end of row
                if (graphemeWidth > 1 && col + graphemeWidth > newWidth)
                {
                    // Leave padding space at end of row
                    row[col] = TerminalCell.Empty;
                    break;
                }

                row[col] = cell;
                col++;
                cellIndex++;

                // Skip continuation cells for wide characters
                for (int w = 1; w < graphemeWidth && col < newWidth; w++)
                {
                    if (cellIndex < cells.Count && string.IsNullOrEmpty(cells[cellIndex].Character))
                    {
                        row[col] = cells[cellIndex];
                        col++;
                        cellIndex++;
                    }
                    else
                    {
                        // Create continuation cell
                        row[col] = TerminalCell.Empty;
                        col++;
                    }
                }
            }

            // Fill remaining columns with empty cells
            for (int x = col; x < newWidth; x++)
                row[x] = TerminalCell.Empty;

            // Set SoftWrap on last cell if there's more content to come
            if (cellIndex < cells.Count)
            {
                row[newWidth - 1] = row[newWidth - 1] with
                {
                    Attributes = row[newWidth - 1].Attributes | CellAttributes.SoftWrap
                };
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Distributes re-wrapped rows into scrollback and screen buffer.
    /// </summary>
    private static ReflowResult DistributeRows(
        List<TerminalCell[]> rewrappedRows,
        ReflowContext context,
        int cursorRow,
        int cursorCol,
        bool preserveCursorRow)
    {
        int screenHeight = context.NewHeight;

        // Trim trailing all-empty rows — they don't carry meaningful content
        // and shouldn't push real content into scrollback.
        int contentRowCount = rewrappedRows.Count;
        while (contentRowCount > 0 && IsEmptyRow(rewrappedRows[contentRowCount - 1]))
            contentRowCount--;

        // Ensure at least enough rows to place the cursor
        contentRowCount = Math.Max(contentRowCount, cursorRow + 1);
        contentRowCount = Math.Min(contentRowCount, rewrappedRows.Count);

        // Determine how many rows go on screen vs scrollback
        int screenStartIndex;

        if (preserveCursorRow)
        {
            // Kitty mode: anchor cursor to its current visual row position if possible
            int desiredCursorScreenRow = Math.Min(context.CursorY, screenHeight - 1);
            screenStartIndex = cursorRow - desiredCursorScreenRow;
            screenStartIndex = Math.Max(0, screenStartIndex);
            screenStartIndex = Math.Min(screenStartIndex, Math.Max(0, contentRowCount - screenHeight));
        }
        else
        {
            // Xterm mode: fill from the bottom
            screenStartIndex = Math.Max(0, contentRowCount - screenHeight);
        }

        // Build scrollback rows
        int scrollbackCount = screenStartIndex;
        var scrollbackRows = new ReflowScrollbackRow[scrollbackCount];
        for (int i = 0; i < scrollbackCount; i++)
        {
            scrollbackRows[i] = new ReflowScrollbackRow(rewrappedRows[i], context.NewWidth);
        }

        // Build screen rows
        var screenRows = new TerminalCell[screenHeight][];
        for (int i = 0; i < screenHeight; i++)
        {
            int sourceIndex = screenStartIndex + i;
            if (sourceIndex < rewrappedRows.Count)
            {
                screenRows[i] = rewrappedRows[sourceIndex];
            }
            else
            {
                var emptyRow = new TerminalCell[context.NewWidth];
                Array.Fill(emptyRow, TerminalCell.Empty);
                screenRows[i] = emptyRow;
            }
        }

        // Adjust cursor position relative to screen
        int newCursorRow = cursorRow - screenStartIndex;
        newCursorRow = Math.Clamp(newCursorRow, 0, screenHeight - 1);
        int newCursorCol = Math.Min(cursorCol, context.NewWidth - 1);

        return new ReflowResult(screenRows, scrollbackRows, newCursorCol, newCursorRow);
    }

    private static bool IsEmptyRow(TerminalCell[] row)
    {
        for (int i = 0; i < row.Length; i++)
        {
            if (!IsEmptyCell(row[i]))
                return false;
        }
        return true;
    }

    private static bool IsEmptyCell(TerminalCell cell)
    {
        return cell.Character is null or " " or ""
            && cell.Foreground is null
            && cell.Background is null
            && (cell.Attributes & ~CellAttributes.SoftWrap) == CellAttributes.None
            && cell.TrackedSixel is null
            && cell.TrackedHyperlink is null;
    }

    private static int GetCellDisplayWidth(TerminalCell cell)
    {
        if (string.IsNullOrEmpty(cell.Character))
            return 1;
        return DisplayWidth.GetGraphemeWidth(cell.Character);
    }
}
