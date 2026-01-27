using System.Globalization;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Surfaces;

/// <summary>
/// A 2D grid of cells representing a rectangular area of terminal content.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Surface"/> provides a buffer for rendering terminal content.
/// It supports direct cell manipulation, text writing with automatic grapheme
/// and wide character handling, and compositing with other surfaces.
/// </para>
/// <para>
/// Surfaces are fixed-size and use row-major storage for cache-efficient iteration.
/// Thread safety is the caller's responsibility - surfaces are designed for
/// high-performance single-threaded rendering.
/// </para>
/// <para>
/// Each surface carries <see cref="CellMetrics"/> that define the pixel dimensions
/// of terminal cells. This is needed for sixel graphics, which are defined in pixels
/// but must be mapped to cell boundaries for proper rendering and clipping.
/// </para>
/// </remarks>
public sealed class Surface : ISurfaceSource
{
    // Row-major storage: cells[y * width + x]
    private readonly SurfaceCell[] _cells;
    
    // Track if any cells contain sixel graphics
    private int _sixelCount;
    
    /// <summary>
    /// Gets the width of the surface in columns.
    /// </summary>
    public int Width { get; }
    
    /// <summary>
    /// Gets the height of the surface in rows.
    /// </summary>
    public int Height { get; }
    
    /// <summary>
    /// Gets the cell metrics for this surface.
    /// </summary>
    /// <remarks>
    /// Cell metrics define the pixel dimensions of terminal cells, which is needed
    /// for sixel graphics operations. When compositing surfaces with sixel content,
    /// both surfaces must have matching cell metrics.
    /// </remarks>
    public CellMetrics CellMetrics { get; }
    
    /// <summary>
    /// Gets whether this surface contains any sixel graphics.
    /// </summary>
    public bool HasSixels => _sixelCount > 0;
    
    /// <summary>
    /// Gets the total number of cells in the surface.
    /// </summary>
    public int CellCount => Width * Height;

    /// <summary>
    /// Creates a new surface with the specified dimensions and default cell metrics.
    /// All cells are initialized to <see cref="SurfaceCells.Empty"/>.
    /// </summary>
    /// <param name="width">The width in columns. Must be positive.</param>
    /// <param name="height">The height in rows. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if width or height is not positive.</exception>
    public Surface(int width, int height) : this(width, height, CellMetrics.Default)
    {
    }

    /// <summary>
    /// Creates a new surface with the specified dimensions and cell metrics.
    /// All cells are initialized to <see cref="SurfaceCells.Empty"/>.
    /// </summary>
    /// <param name="width">The width in columns. Must be positive.</param>
    /// <param name="height">The height in rows. Must be positive.</param>
    /// <param name="cellMetrics">The pixel dimensions of terminal cells.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if width or height is not positive.</exception>
    public Surface(int width, int height, CellMetrics cellMetrics)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        
        Width = width;
        Height = height;
        CellMetrics = cellMetrics;
        _cells = new SurfaceCell[width * height];
        
        // Initialize all cells to empty
        Array.Fill(_cells, SurfaceCells.Empty);
    }

    /// <summary>
    /// Gets or sets the cell at the specified position.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <returns>The cell at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the position is outside the surface bounds.</exception>
    public SurfaceCell this[int x, int y]
    {
        get
        {
            ValidateBounds(x, y);
            return _cells[y * Width + x];
        }
        set
        {
            ValidateBounds(x, y);
            var index = y * Width + x;
            
            // Track sixel count changes
            var oldCell = _cells[index];
            if (oldCell.HasSixel && !value.HasSixel)
                _sixelCount--;
            else if (!oldCell.HasSixel && value.HasSixel)
                _sixelCount++;
            
            _cells[index] = value;
        }
    }

    /// <inheritdoc />
    public SurfaceCell GetCell(int x, int y)
    {
        ValidateBounds(x, y);
        return _cells[y * Width + x];
    }

    /// <summary>
    /// Tries to get the cell at the specified position.
    /// Returns false if the position is outside the surface bounds.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <param name="cell">The cell at the position, or default if out of bounds.</param>
    /// <returns>True if the position is valid, false otherwise.</returns>
    public bool TryGetCell(int x, int y, out SurfaceCell cell)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            cell = _cells[y * Width + x];
            return true;
        }
        cell = default;
        return false;
    }

    /// <summary>
    /// Tries to set the cell at the specified position.
    /// Returns false if the position is outside the surface bounds.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <param name="cell">The cell to set.</param>
    /// <returns>True if the position is valid and the cell was set, false otherwise.</returns>
    public bool TrySetCell(int x, int y, SurfaceCell cell)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            var index = y * Width + x;
            
            // Track sixel count changes
            var oldCell = _cells[index];
            if (oldCell.HasSixel && !cell.HasSixel)
                _sixelCount--;
            else if (!oldCell.HasSixel && cell.HasSixel)
                _sixelCount++;
            
            _cells[index] = cell;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if the specified position is within the surface bounds.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <returns>True if the position is within bounds, false otherwise.</returns>
    public bool IsInBounds(int x, int y)
        => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Clears all cells to <see cref="SurfaceCells.Empty"/>.
    /// </summary>
    public void Clear()
    {
        Array.Fill(_cells, SurfaceCells.Empty);
        _sixelCount = 0;
    }

    /// <summary>
    /// Clears all cells to the specified cell value.
    /// </summary>
    /// <param name="cell">The cell value to fill with.</param>
    public void Clear(SurfaceCell cell)
    {
        Array.Fill(_cells, cell);
        _sixelCount = cell.HasSixel ? CellCount : 0;
    }

    /// <summary>
    /// Fills a rectangular region with the specified cell value.
    /// The region is clipped to the surface bounds.
    /// </summary>
    /// <param name="region">The region to fill.</param>
    /// <param name="cell">The cell value to fill with.</param>
    public void Fill(Rect region, SurfaceCell cell)
    {
        // Clip to surface bounds
        var startX = Math.Max(0, region.X);
        var startY = Math.Max(0, region.Y);
        var endX = Math.Min(Width, region.Right);
        var endY = Math.Min(Height, region.Bottom);

        for (var y = startY; y < endY; y++)
        {
            var rowStart = y * Width;
            for (var x = startX; x < endX; x++)
            {
                var index = rowStart + x;
                
                // Track sixel count changes
                var oldCell = _cells[index];
                if (oldCell.HasSixel && !cell.HasSixel)
                    _sixelCount--;
                else if (!oldCell.HasSixel && cell.HasSixel)
                    _sixelCount++;
                
                _cells[index] = cell;
            }
        }
    }

    /// <summary>
    /// Writes text to the surface starting at the specified position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Text is written left-to-right, respecting grapheme cluster boundaries.
    /// Wide characters (CJK, emoji) automatically set continuation cells.
    /// Text that extends beyond the surface width is clipped.
    /// </para>
    /// <para>
    /// Newlines and other control characters are not handled - use multiple
    /// <see cref="WriteText"/> calls for multi-line text.
    /// </para>
    /// </remarks>
    /// <param name="x">The starting column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <param name="text">The text to write.</param>
    /// <param name="foreground">The foreground color, or null for transparent.</param>
    /// <param name="background">The background color, or null for transparent.</param>
    /// <param name="attributes">Text styling attributes.</param>
    /// <returns>The number of columns written (including continuations).</returns>
    public int WriteText(int x, int y, string text, Hex1bColor? foreground = null, Hex1bColor? background = null, CellAttributes attributes = CellAttributes.None)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Early exit if completely outside bounds
        if (y < 0 || y >= Height || x >= Width)
            return 0;

        var currentX = x;
        var columnsWritten = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);

        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);

            // Skip if starting position is negative (partial clipping on left)
            if (currentX < 0)
            {
                currentX += graphemeWidth;
                continue;
            }

            // Stop if we've gone past the right edge
            if (currentX >= Width)
                break;

            // Check if the full grapheme fits
            if (currentX + graphemeWidth > Width)
            {
                // Wide character doesn't fully fit - clip it
                // Fill remaining space with spaces
                while (currentX < Width)
                {
                    _cells[y * Width + currentX] = new SurfaceCell(" ", foreground, background, attributes, 1);
                    currentX++;
                    columnsWritten++;
                }
                break;
            }

            // Write the primary cell
            var cell = new SurfaceCell(grapheme, foreground, background, attributes, graphemeWidth);
            _cells[y * Width + currentX] = cell;
            currentX++;
            columnsWritten++;

            // Write continuation cells for wide characters
            for (var i = 1; i < graphemeWidth; i++)
            {
                if (currentX < Width)
                {
                    _cells[y * Width + currentX] = SurfaceCell.CreateContinuation(background);
                    currentX++;
                    columnsWritten++;
                }
            }
        }

        return columnsWritten;
    }

    /// <summary>
    /// Writes a single character to the surface at the specified position.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <param name="character">The character to write.</param>
    /// <param name="foreground">The foreground color, or null for transparent.</param>
    /// <param name="background">The background color, or null for transparent.</param>
    /// <param name="attributes">Text styling attributes.</param>
    /// <returns>True if the character was written, false if the position was out of bounds.</returns>
    public bool WriteChar(int x, int y, char character, Hex1bColor? foreground = null, Hex1bColor? background = null, CellAttributes attributes = CellAttributes.None)
    {
        if (!IsInBounds(x, y))
            return false;

        _cells[y * Width + x] = new SurfaceCell(character.ToString(), foreground, background, attributes, 1);
        return true;
    }

    /// <summary>
    /// Composites another surface source onto this surface at the specified offset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cells from the source are copied onto this surface.
    /// Transparent backgrounds in the source allow the destination's background to show through.
    /// The operation is clipped to both the destination bounds and the optional clip rectangle.
    /// </para>
    /// <para>
    /// If the source is a <see cref="CompositeSurface"/>, its layers are resolved on demand
    /// as cells are accessed.
    /// </para>
    /// <para>
    /// When the source contains sixel graphics, both surfaces must have matching
    /// <see cref="CellMetrics"/>. This ensures sixel pixel coordinates map correctly
    /// to cell boundaries.
    /// </para>
    /// </remarks>
    /// <param name="source">The source to composite (Surface, CompositeSurface, or any ISurfaceSource).</param>
    /// <param name="offsetX">The X offset in this surface where the source's (0,0) will be placed.</param>
    /// <param name="offsetY">The Y offset in this surface where the source's (0,0) will be placed.</param>
    /// <param name="clip">Optional clip rectangle in destination coordinates. If null, uses entire destination.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the source contains sixel graphics but has different cell metrics than this surface.
    /// </exception>
    public void Composite(ISurfaceSource source, int offsetX, int offsetY, Rect? clip = null)
    {
        // Validate cell metrics if source has sixels
        if (source.HasSixels && source.CellMetrics != CellMetrics)
        {
            throw new InvalidOperationException(
                $"Cannot composite surface with sixels when CellMetrics differ. " +
                $"Target: {CellMetrics}, Source: {source.CellMetrics}");
        }

        // Determine the effective clip region
        var clipRect = clip ?? new Rect(0, 0, Width, Height);
        
        // Intersect clip with destination bounds
        var destStartX = Math.Max(0, Math.Max(offsetX, clipRect.X));
        var destStartY = Math.Max(0, Math.Max(offsetY, clipRect.Y));
        var destEndX = Math.Min(Width, Math.Min(offsetX + source.Width, clipRect.Right));
        var destEndY = Math.Min(Height, Math.Min(offsetY + source.Height, clipRect.Bottom));

        // Early exit if no overlap
        if (destStartX >= destEndX || destStartY >= destEndY)
            return;

        for (var destY = destStartY; destY < destEndY; destY++)
        {
            var srcY = destY - offsetY;
            var destRowStart = destY * Width;

            for (var destX = destStartX; destX < destEndX; destX++)
            {
                var srcX = destX - offsetX;
                var srcCell = source.GetCell(srcX, srcY);
                
                // Handle transparency
                if (srcCell.HasTransparentBackground)
                {
                    // Blend with existing cell's background
                    var destCell = _cells[destRowStart + destX];
                    srcCell = srcCell with { Background = destCell.Background };
                }

                var index = destRowStart + destX;
                
                // Track sixel count changes
                var oldCell = _cells[index];
                if (oldCell.HasSixel && !srcCell.HasSixel)
                    _sixelCount--;
                else if (!oldCell.HasSixel && srcCell.HasSixel)
                    _sixelCount++;

                _cells[index] = srcCell;
            }
        }
    }

    /// <summary>
    /// Gets a read-only span over all cells in row-major order.
    /// </summary>
    /// <returns>A span of all cells.</returns>
    public ReadOnlySpan<SurfaceCell> AsSpan() => _cells.AsSpan();

    /// <summary>
    /// Gets a read-only span over the cells in a specific row.
    /// </summary>
    /// <param name="row">The row index (0-based).</param>
    /// <returns>A span of cells in the specified row.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the row is out of bounds.</exception>
    public ReadOnlySpan<SurfaceCell> GetRow(int row)
    {
        if (row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException(nameof(row), row, $"Row must be between 0 and {Height - 1}");
        
        return _cells.AsSpan(row * Width, Width);
    }

    /// <summary>
    /// Creates a deep copy of this surface.
    /// </summary>
    /// <returns>A new surface with the same dimensions, cell metrics, and cell values.</returns>
    public Surface Clone()
    {
        var clone = new Surface(Width, Height, CellMetrics);
        _cells.AsSpan().CopyTo(clone._cells);
        clone._sixelCount = _sixelCount;
        return clone;
    }

    /// <summary>
    /// Gets the underlying cell array for direct access.
    /// </summary>
    /// <remarks>
    /// This is an internal optimization for cases where Span cannot be used
    /// (e.g., in async contexts). Use with caution - no bounds checking.
    /// </remarks>
    internal SurfaceCell[] CellsUnsafe => _cells;

    private void ValidateBounds(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X must be between 0 and {Width - 1}");
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y must be between 0 and {Height - 1}");
    }
}
