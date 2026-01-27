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
/// Common cell sizes:
/// <list type="bullet">
///   <item>10×20 - typical for many terminals (default)</item>
///   <item>9×18 - common for smaller fonts</item>
///   <item>8×16 - classic VGA text mode</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="PixelWidth">The width of a cell in pixels.</param>
/// <param name="PixelHeight">The height of a cell in pixels.</param>
public readonly record struct CellMetrics(int PixelWidth, int PixelHeight)
{
    /// <summary>
    /// Default cell metrics (10×20 pixels).
    /// </summary>
    public static readonly CellMetrics Default = new(10, 20);

    /// <summary>
    /// Converts a cell rectangle to pixel coordinates.
    /// </summary>
    /// <param name="cellX">Cell X position.</param>
    /// <param name="cellY">Cell Y position.</param>
    /// <param name="cellWidth">Width in cells.</param>
    /// <param name="cellHeight">Height in cells.</param>
    /// <returns>The equivalent rectangle in pixel coordinates.</returns>
    public PixelRect CellToPixel(int cellX, int cellY, int cellWidth, int cellHeight)
        => new(cellX * PixelWidth, cellY * PixelHeight,
               cellWidth * PixelWidth, cellHeight * PixelHeight);

    /// <summary>
    /// Converts a cell rectangle to pixel coordinates.
    /// </summary>
    /// <param name="cellRect">Rectangle in cell coordinates.</param>
    /// <returns>The equivalent rectangle in pixel coordinates.</returns>
    public PixelRect CellToPixel(Rect cellRect)
        => CellToPixel(cellRect.X, cellRect.Y, cellRect.Width, cellRect.Height);

    /// <summary>
    /// Computes the cell span for a pixel dimension (rounds up).
    /// </summary>
    /// <param name="pixelWidth">Width in pixels.</param>
    /// <param name="pixelHeight">Height in pixels.</param>
    /// <returns>The cell span (width and height in cells).</returns>
    public (int CellWidth, int CellHeight) PixelToCellSpan(int pixelWidth, int pixelHeight)
        => ((pixelWidth + PixelWidth - 1) / PixelWidth,
            (pixelHeight + PixelHeight - 1) / PixelHeight);

    /// <summary>
    /// Converts a pixel rectangle to cell coordinates.
    /// The result uses ceiling for width/height to ensure full coverage.
    /// </summary>
    /// <param name="pixelRect">Rectangle in pixel coordinates.</param>
    /// <returns>The equivalent rectangle in cell coordinates.</returns>
    public Rect PixelToCell(PixelRect pixelRect)
    {
        var (cellWidth, cellHeight) = PixelToCellSpan(pixelRect.Width, pixelRect.Height);
        return new Rect(
            pixelRect.X / PixelWidth,
            pixelRect.Y / PixelHeight,
            cellWidth,
            cellHeight);
    }
}
