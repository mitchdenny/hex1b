namespace Hex1b;

/// <summary>
/// Specifies how text is selected within a terminal buffer.
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// Character-level selection: selects contiguous characters from anchor to cursor,
    /// wrapping across rows.
    /// </summary>
    Character,

    /// <summary>
    /// Line-level selection: selects entire rows from the anchor row to the cursor row.
    /// </summary>
    Line,

    /// <summary>
    /// Block/rectangular selection: selects a rectangle defined by the anchor and cursor
    /// columns across all rows between them.
    /// </summary>
    Block
}

/// <summary>
/// Represents a position in the terminal's virtual buffer, which unifies
/// scrollback rows (numbered 0..N-1, oldest to newest) and screen rows
/// (numbered N..N+H-1).
/// </summary>
/// <param name="Row">The virtual row index (0-based, scrollback rows first, then screen rows).</param>
/// <param name="Column">The column index (0-based).</param>
public readonly record struct BufferPosition(int Row, int Column) : IComparable<BufferPosition>
{
    /// <inheritdoc />
    public int CompareTo(BufferPosition other)
    {
        int rowCmp = Row.CompareTo(other.Row);
        return rowCmp != 0 ? rowCmp : Column.CompareTo(other.Column);
    }

    /// <summary>Returns true if this position is before the other position.</summary>
    public bool IsBefore(BufferPosition other) => CompareTo(other) < 0;

    /// <summary>Returns true if this position is after the other position.</summary>
    public bool IsAfter(BufferPosition other) => CompareTo(other) > 0;

    /// <inheritdoc />
    public static bool operator <(BufferPosition left, BufferPosition right) => left.CompareTo(right) < 0;
    /// <inheritdoc />
    public static bool operator >(BufferPosition left, BufferPosition right) => left.CompareTo(right) > 0;
    /// <inheritdoc />
    public static bool operator <=(BufferPosition left, BufferPosition right) => left.CompareTo(right) <= 0;
    /// <inheritdoc />
    public static bool operator >=(BufferPosition left, BufferPosition right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Tracks the current text selection state within a terminal buffer.
/// </summary>
/// <remarks>
/// The selection operates on a "virtual buffer" that unifies scrollback and screen rows.
/// Scrollback rows are indexed 0..scrollbackCount-1, and screen rows are indexed
/// scrollbackCount..scrollbackCount+screenHeight-1.
/// </remarks>
public sealed class TerminalSelection
{
    /// <summary>
    /// The position where the selection was started.
    /// </summary>
    public BufferPosition Anchor { get; private set; }

    /// <summary>
    /// The current cursor position (the moving end of the selection).
    /// </summary>
    public BufferPosition Cursor { get; private set; }

    /// <summary>
    /// The selection mode (character, line, or block).
    /// </summary>
    public SelectionMode Mode { get; private set; }

    /// <summary>
    /// Whether a selection range is active (anchor has been set and cursor is moving).
    /// When false, only the cursor is positioned but no text is selected.
    /// </summary>
    public bool IsSelecting { get; private set; }

    /// <summary>
    /// Creates a new selection with the cursor at the specified position.
    /// No text is selected until <see cref="StartSelection"/> is called.
    /// </summary>
    public TerminalSelection(BufferPosition initialPosition)
    {
        Cursor = initialPosition;
        Anchor = initialPosition;
    }

    /// <summary>
    /// Moves the cursor to the specified position. If selecting, extends the selection.
    /// </summary>
    public void MoveCursor(BufferPosition position)
    {
        Cursor = position;
    }

    /// <summary>
    /// Begins a selection at the current cursor position.
    /// </summary>
    public void StartSelection(SelectionMode mode = SelectionMode.Character)
    {
        Anchor = Cursor;
        Mode = mode;
        IsSelecting = true;
    }

    /// <summary>
    /// Toggles between selection modes. If the requested mode is already active,
    /// switches back to character mode.
    /// </summary>
    public void ToggleMode(SelectionMode mode)
    {
        if (!IsSelecting)
        {
            StartSelection(mode);
            return;
        }

        Mode = Mode == mode ? SelectionMode.Character : mode;
    }

    /// <summary>
    /// Clears the selection but keeps the cursor position.
    /// </summary>
    public void ClearSelection()
    {
        IsSelecting = false;
    }

    /// <summary>
    /// Gets the start (earlier) position of the selection.
    /// </summary>
    public BufferPosition Start => Anchor <= Cursor ? Anchor : Cursor;

    /// <summary>
    /// Gets the end (later) position of the selection.
    /// </summary>
    public BufferPosition End => Anchor <= Cursor ? Cursor : Anchor;

    /// <summary>
    /// Determines whether the specified cell position is within the current selection.
    /// </summary>
    /// <param name="row">The virtual row index.</param>
    /// <param name="column">The column index.</param>
    /// <returns>True if the cell is selected.</returns>
    public bool IsCellSelected(int row, int column)
    {
        if (!IsSelecting) return false;

        var start = Start;
        var end = End;

        return Mode switch
        {
            SelectionMode.Character => IsCellInCharacterSelection(row, column, start, end),
            SelectionMode.Line => row >= start.Row && row <= end.Row,
            SelectionMode.Block => IsCellInBlockSelection(row, column, start, end),
            _ => false
        };
    }

    private static bool IsCellInCharacterSelection(int row, int column, BufferPosition start, BufferPosition end)
    {
        if (row < start.Row || row > end.Row) return false;
        if (start.Row == end.Row) return column >= start.Column && column <= end.Column;
        if (row == start.Row) return column >= start.Column;
        if (row == end.Row) return column <= end.Column;
        return true; // middle rows are fully selected
    }

    private static bool IsCellInBlockSelection(int row, int column, BufferPosition start, BufferPosition end)
    {
        if (row < start.Row || row > end.Row) return false;
        int minCol = Math.Min(start.Column, end.Column);
        int maxCol = Math.Max(start.Column, end.Column);
        return column >= minCol && column <= maxCol;
    }

    /// <summary>
    /// Extracts the selected text from a virtual buffer.
    /// </summary>
    /// <param name="getCell">Function to get a cell at (virtualRow, column). Returns null for out-of-bounds.</param>
    /// <param name="bufferWidth">The width of the buffer (columns per row).</param>
    /// <returns>The selected text, or null if no selection is active.</returns>
    public string? ExtractText(Func<int, int, TerminalCell?> getCell, int bufferWidth)
    {
        if (!IsSelecting) return null;

        var start = Start;
        var end = End;

        return Mode switch
        {
            SelectionMode.Character => ExtractCharacterText(getCell, bufferWidth, start, end),
            SelectionMode.Line => ExtractLineText(getCell, bufferWidth, start.Row, end.Row),
            SelectionMode.Block => ExtractBlockText(getCell, start, end),
            _ => null
        };
    }

    private static string ExtractCharacterText(
        Func<int, int, TerminalCell?> getCell, int bufferWidth,
        BufferPosition start, BufferPosition end)
    {
        var sb = new System.Text.StringBuilder();

        for (int row = start.Row; row <= end.Row; row++)
        {
            int startCol = row == start.Row ? start.Column : 0;
            int endCol = row == end.Row ? end.Column : bufferWidth - 1;

            // Check for soft wrap on the last cell of this row
            bool isSoftWrapped = false;
            if (row < end.Row)
            {
                var lastCell = getCell(row, bufferWidth - 1);
                isSoftWrapped = lastCell?.IsSoftWrap ?? false;
            }

            AppendRowSegment(sb, getCell, row, startCol, endCol, trimTrailing: !isSoftWrapped);

            // Add newline between rows unless soft-wrapped
            if (row < end.Row && !isSoftWrapped)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string ExtractLineText(
        Func<int, int, TerminalCell?> getCell, int bufferWidth,
        int startRow, int endRow)
    {
        var sb = new System.Text.StringBuilder();

        for (int row = startRow; row <= endRow; row++)
        {
            bool isSoftWrapped = false;
            if (row < endRow)
            {
                var lastCell = getCell(row, bufferWidth - 1);
                isSoftWrapped = lastCell?.IsSoftWrap ?? false;
            }

            AppendRowSegment(sb, getCell, row, 0, bufferWidth - 1, trimTrailing: !isSoftWrapped);

            if (row < endRow && !isSoftWrapped)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string ExtractBlockText(
        Func<int, int, TerminalCell?> getCell,
        BufferPosition start, BufferPosition end)
    {
        var sb = new System.Text.StringBuilder();
        int minCol = Math.Min(start.Column, end.Column);
        int maxCol = Math.Max(start.Column, end.Column);

        for (int row = start.Row; row <= end.Row; row++)
        {
            AppendRowSegment(sb, getCell, row, minCol, maxCol);

            if (row < end.Row)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void AppendRowSegment(
        System.Text.StringBuilder sb, Func<int, int, TerminalCell?> getCell,
        int row, int startCol, int endCol, bool trimTrailing = true)
    {
        // Build the row content
        int segmentStart = sb.Length;

        for (int col = startCol; col <= endCol; col++)
        {
            var cell = getCell(row, col);
            if (cell == null) break;

            var ch = cell.Value.Character;

            // Skip wide character padding cells (empty string or null-like)
            if (string.IsNullOrEmpty(ch)) continue;

            sb.Append(ch);
        }

        // Trim trailing whitespace from this row segment
        if (trimTrailing)
        {
            int segmentEnd = sb.Length;
            while (segmentEnd > segmentStart && sb[segmentEnd - 1] == ' ')
            {
                segmentEnd--;
            }
            sb.Length = segmentEnd;
        }
    }
}
