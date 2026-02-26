namespace ModelViewerDemo;

/// <summary>
/// A pixel buffer that maps a 2D dot grid to Unicode braille characters (U+2800–U+28FF).
/// Each terminal cell covers a 2×4 dot sub-grid, giving 8 dots per character.
/// </summary>
internal sealed class BrailleBuffer
{
    // Braille dot bit positions for a 2×4 grid:
    //   Col 0  Col 1
    //   0x01   0x08   row 0
    //   0x02   0x10   row 1
    //   0x04   0x20   row 2
    //   0x40   0x80   row 3
    private static readonly byte[] DotBits =
    [
        0x01, 0x02, 0x04, 0x40, // column 0, rows 0-3
        0x08, 0x10, 0x20, 0x80  // column 1, rows 0-3
    ];

    private readonly byte[] _cells;

    /// <summary>Width in terminal cells.</summary>
    public int CellWidth { get; }

    /// <summary>Height in terminal cells.</summary>
    public int CellHeight { get; }

    /// <summary>Width in dot (sub-pixel) coordinates.</summary>
    public int DotWidth => CellWidth * 2;

    /// <summary>Height in dot (sub-pixel) coordinates.</summary>
    public int DotHeight => CellHeight * 4;

    public BrailleBuffer(int cellWidth, int cellHeight)
    {
        CellWidth = cellWidth;
        CellHeight = cellHeight;
        _cells = new byte[cellWidth * cellHeight];
    }

    public void Clear()
    {
        Array.Clear(_cells);
    }

    /// <summary>
    /// Set a single dot at the given dot-space coordinates.
    /// </summary>
    public void SetDot(int dotX, int dotY)
    {
        if (dotX < 0 || dotX >= DotWidth || dotY < 0 || dotY >= DotHeight)
            return;

        int cellX = dotX / 2;
        int cellY = dotY / 4;
        int subX = dotX % 2;
        int subY = dotY % 4;

        _cells[cellY * CellWidth + cellX] |= DotBits[subX * 4 + subY];
    }

    /// <summary>
    /// Draw a line between two dot-space points using Bresenham's algorithm.
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetDot(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>
    /// Get the braille character for a terminal cell, or null if empty.
    /// </summary>
    public char? GetChar(int cellX, int cellY)
    {
        if (cellX < 0 || cellX >= CellWidth || cellY < 0 || cellY >= CellHeight)
            return null;

        byte pattern = _cells[cellY * CellWidth + cellX];
        return pattern == 0 ? null : (char)(0x2800 + pattern);
    }
}
