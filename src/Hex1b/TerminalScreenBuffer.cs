namespace Hex1b;

/// <summary>
/// Owns the rectangular cells and rendition metadata for one terminal screen.
/// </summary>
internal sealed class TerminalScreenBuffer
{
    private readonly TerminalCell[,] _cells;
    private readonly LineRendition[] _renditions;

    public TerminalScreenBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new TerminalCell[height, width];
        _renditions = new LineRendition[height];
        for (var row = 0; row < height; row++)
            for (var column = 0; column < width; column++)
                _cells[row, column] = TerminalCell.Empty;
    }

    public int Width { get; }
    public int Height { get; }
    // Borrowed by graphics projection; row mutations go through the terminal's row operations.
    public TerminalCell[,] Cells => _cells;
    public IReadOnlyList<LineRendition> Renditions => _renditions;
    public ref TerminalCell this[int row, int column] => ref _cells[row, column];
    public LineRendition GetRendition(int row) => _renditions[row];
    public void SetRendition(int row, LineRendition rendition) => _renditions[row] = rendition;
    public void ResetRenditions() => Array.Clear(_renditions);
    public int LogicalWidth(int row) =>
        _renditions[row] == LineRendition.SingleWidth ? Width : Math.Max(1, Width / 2);

    public LineRendition[] CopyRenditions() => (LineRendition[])_renditions.Clone();
    public TerminalCell[,] CopyCells() => (TerminalCell[,])_cells.Clone();

    public void CopyCellsTo(TerminalCell[,] destination) =>
        Array.Copy(_cells, destination, _cells.Length);

    public void CopyRow(int source, int destination, int left, int right)
    {
        for (var column = left; column <= right; column++)
            _cells[source, column].TrackedHyperlink?.AddRef();
        for (var column = left; column <= right; column++)
            _cells[destination, column].TrackedHyperlink?.Release();
        Array.Copy(_cells, source * Width + left, _cells, destination * Width + left, right - left + 1);
    }

    // A saved screen is an independent owner, unlike a borrowed cell export.
    public TerminalScreenBuffer Clone()
    {
        var copy = new TerminalScreenBuffer(Width, Height);
        Array.Copy(_renditions, copy._renditions, Height);
        Array.Copy(_cells, copy._cells, _cells.Length);
        for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
                copy[row, column].TrackedHyperlink?.AddRef();
        return copy;
    }

    public void ReleaseHyperlinks()
    {
        for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
                _cells[row, column].TrackedHyperlink?.Release();
    }
}
