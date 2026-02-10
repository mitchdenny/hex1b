namespace Hex1b.Charts;

/// <summary>
/// A 2D bitmap that maps to Unicode braille characters (U+2800–U+28FF).
/// Each terminal cell represents a 2×4 dot grid, providing 8 sub-pixel points per cell.
/// </summary>
/// <remarks>
/// <para>
/// Braille dot positions within a cell (col 0-1, row 0-3):
/// </para>
/// <code>
///   Col 0  Col 1
///   ⠁(0)   ⠈(3)   row 0
///   ⠂(1)   ⠐(4)   row 1
///   ⠄(2)   ⠠(5)   row 2
///   ⡀(6)   ⢀(7)   row 3
/// </code>
/// <para>
/// The bit index for each position: left column = bits 0,1,2,6; right column = bits 3,4,5,7.
/// The braille character is U+2800 + the combined bit pattern.
/// </para>
/// </remarks>
internal sealed class BrailleCanvas
{
    // Bit positions for each dot in a 2×4 cell
    // [col, row] → bit index
    private static readonly int[,] DotBits =
    {
        { 0, 1, 2, 6 }, // col 0: rows 0-3
        { 3, 4, 5, 7 }, // col 1: rows 0-3
    };

    private readonly int _cellWidth;
    private readonly int _cellHeight;
    private readonly byte[,] _cells; // Each byte stores the 8-bit braille pattern for one cell

    /// <summary>
    /// Gets the width in terminal cells.
    /// </summary>
    public int CellWidth => _cellWidth;

    /// <summary>
    /// Gets the height in terminal cells.
    /// </summary>
    public int CellHeight => _cellHeight;

    /// <summary>
    /// Gets the width in dot coordinates (2× cell width).
    /// </summary>
    public int DotWidth => _cellWidth * 2;

    /// <summary>
    /// Gets the height in dot coordinates (4× cell height).
    /// </summary>
    public int DotHeight => _cellHeight * 4;

    /// <summary>
    /// Creates a new braille canvas with the specified cell dimensions.
    /// </summary>
    /// <param name="cellWidth">Width in terminal cells.</param>
    /// <param name="cellHeight">Height in terminal cells.</param>
    public BrailleCanvas(int cellWidth, int cellHeight)
    {
        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        _cells = new byte[cellWidth, cellHeight];
    }

    /// <summary>
    /// Sets a dot at the given dot coordinates.
    /// </summary>
    /// <param name="dotX">X position in dot space (0 to DotWidth-1).</param>
    /// <param name="dotY">Y position in dot space (0 to DotHeight-1).</param>
    public void SetDot(int dotX, int dotY)
    {
        if (dotX < 0 || dotX >= DotWidth || dotY < 0 || dotY >= DotHeight)
            return;

        var cellX = dotX / 2;
        var cellY = dotY / 4;
        var col = dotX % 2;
        var row = dotY % 4;

        _cells[cellX, cellY] |= (byte)(1 << DotBits[col, row]);
    }

    /// <summary>
    /// Gets the braille character for the given cell position.
    /// Returns null if the cell has no dots set.
    /// </summary>
    /// <param name="cellX">X position in cell space.</param>
    /// <param name="cellY">Y position in cell space.</param>
    /// <returns>The braille character, or null if empty.</returns>
    public char? GetCell(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= _cellWidth || cellY < 0 || cellY >= _cellHeight)
            return null;

        var pattern = _cells[cellX, cellY];
        if (pattern == 0)
            return null;

        return (char)(0x2800 + pattern);
    }

    /// <summary>
    /// Gets the raw bit pattern for a cell (for OR operations).
    /// </summary>
    public byte GetPattern(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= _cellWidth || cellY < 0 || cellY >= _cellHeight)
            return 0;
        return _cells[cellX, cellY];
    }

    /// <summary>
    /// OR-merges another canvas into this one. Both canvases must have the same dimensions.
    /// </summary>
    public void Or(BrailleCanvas other)
    {
        var w = Math.Min(_cellWidth, other._cellWidth);
        var h = Math.Min(_cellHeight, other._cellHeight);
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            _cells[x, y] |= other._cells[x, y];
    }

    /// <summary>
    /// Clears all dots.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_cells);
    }

    /// <summary>
    /// Draws a line between two dot-space coordinates using Bresenham's algorithm.
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            SetDot(x0, y0);

            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Fills all dots below a given Y coordinate in a specific column (dot-space X).
    /// Used for area fill in time series charts.
    /// </summary>
    /// <param name="dotX">The X position in dot space.</param>
    /// <param name="dotYTop">The top Y position (inclusive) — dots are filled from here to the bottom.</param>
    public void FillBelow(int dotX, int dotYTop)
    {
        if (dotX < 0 || dotX >= DotWidth)
            return;

        var maxY = DotHeight;
        for (int y = Math.Max(0, dotYTop); y < maxY; y++)
            SetDot(dotX, y);
    }
}
