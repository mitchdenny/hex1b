using Hex1b.Layout;

namespace Hex1b.Surfaces;

/// <summary>
/// Represents the pixel dimensions of a terminal cell.
/// </summary>
/// <remarks>
/// <para>
/// Terminal cells have a fixed pixel size determined by the font and terminal emulator.
/// This information is needed for sixel graphics, which are defined in pixels but must
/// be mapped to cell boundaries for proper rendering and clipping.
/// </para>
/// <para>
/// Note: Cell width may be fractional in browser-based terminals like xterm.js due to
/// font rendering. The <see cref="ActualPixelWidth"/> property stores the precise value
/// while <see cref="PixelWidth"/> provides the integer approximation for compatibility.
/// </para>
/// </remarks>
public readonly record struct CellMetrics
{
    /// <summary>
    /// The integer width of a cell in pixels (for backward compatibility).
    /// </summary>
    public int PixelWidth { get; }
    
    /// <summary>
    /// The integer height of a cell in pixels.
    /// </summary>
    public int PixelHeight { get; }
    
    /// <summary>
    /// The actual (possibly fractional) width of a cell in pixels.
    /// Used for precise sixel sizing in browser-based terminals.
    /// </summary>
    public double ActualPixelWidth { get; }
    
    /// <summary>
    /// Creates cell metrics with integer dimensions.
    /// </summary>
    public CellMetrics(int pixelWidth, int pixelHeight)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        ActualPixelWidth = pixelWidth;
    }
    
    /// <summary>
    /// Creates cell metrics with actual (floating-point) width.
    /// </summary>
    public CellMetrics(double actualPixelWidth, int pixelHeight)
    {
        ActualPixelWidth = actualPixelWidth;
        PixelWidth = (int)Math.Round(actualPixelWidth);
        PixelHeight = pixelHeight;
    }

    /// <summary>
    /// Default cell metrics (10×20 pixels).
    /// </summary>
    public static readonly CellMetrics Default = new(10, 20);
    
    /// <summary>
    /// Cell metrics for xterm.js with 14px font (approximately 9×17 pixels).
    /// </summary>
    public static readonly CellMetrics XtermJs = new(9, 17);

    /// <summary>
    /// Calculates the pixel width for a given number of cells using actual dimensions.
    /// </summary>
    public int GetPixelWidthForCells(int cellCount)
        => (int)Math.Round(cellCount * ActualPixelWidth);
    
    /// <summary>
    /// Calculates the cell offset for a given pixel position using actual dimensions.
    /// </summary>
    public int GetCellOffsetForPixel(int pixelX)
        => (int)Math.Floor(pixelX / ActualPixelWidth);
    
    /// <summary>
    /// Calculates the pixel position for a cell boundary using actual dimensions.
    /// </summary>
    public int GetPixelForCellBoundary(int cellX)
        => (int)Math.Round(cellX * ActualPixelWidth);

    /// <summary>
    /// Converts a cell rectangle to pixel coordinates.
    /// </summary>
    public PixelRect CellToPixel(int cellX, int cellY, int cellWidth, int cellHeight)
        => new(GetPixelForCellBoundary(cellX), cellY * PixelHeight,
               GetPixelWidthForCells(cellWidth), cellHeight * PixelHeight);

    /// <summary>
    /// Converts a cell rectangle to pixel coordinates.
    /// </summary>
    public PixelRect CellToPixel(Rect cellRect)
        => CellToPixel(cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

    /// <summary>
    /// Computes the cell span for a pixel dimension (rounds up).
    /// </summary>
    public (int CellWidth, int CellHeight) PixelToCellSpan(int pixelWidth, int pixelHeight)
        => ((int)Math.Ceiling(pixelWidth / ActualPixelWidth),
            (pixelHeight + PixelHeight - 1) / PixelHeight);

    /// <summary>
    /// Converts a pixel rectangle to cell coordinates.
    /// </summary>
    public Rect PixelToCell(PixelRect pixelRect)
    {
        var (cellWidth, cellHeight) = PixelToCellSpan(pixelRect.Width, pixelRect.Height);
        return new Rect(
            GetCellOffsetForPixel(pixelRect.X),
            pixelRect.Y / PixelHeight,
            cellWidth,
            cellHeight);
    }
}
