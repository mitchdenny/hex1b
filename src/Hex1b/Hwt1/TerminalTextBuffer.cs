namespace Hex1b;

// A bounded, lock-scoped view of producer text. Never retain this beyond the buffer lock.
internal sealed class TerminalTextBuffer(
    long generation, bool alternate, int width, int height, int historyCount,
    Func<int, long> rowId, Func<int, int, TerminalCell> getCell, Func<int, int> rowWidth)
{
    internal long Generation => generation;
    internal bool Alternate => alternate;
    internal int Width => width;
    internal int Height => height;
    internal int HistoryCount => historyCount;
    internal int TotalRows => historyCount + height;
    internal long RowId(int row) => rowId(row);
    internal TerminalCell Cell(int row, int column) => getCell(row, column);
    internal int RowWidth(int row) => rowWidth(row);
    internal bool SoftWrap(int row) => row >= 0 && row < TotalRows && Cell(row, rowWidth(row) - 1).IsSoftWrap;

    internal int FindRow(long id)
    {
        for (var row = 0; row < TotalRows; row++)
            if (rowId(row) == id)
                return row;
        return -1;
    }
}
