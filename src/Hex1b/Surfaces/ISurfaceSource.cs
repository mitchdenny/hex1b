namespace Hex1b.Surfaces;

/// <summary>
/// Common interface for any source that can provide terminal cells.
/// </summary>
/// <remarks>
/// <para>
/// This interface enables both <see cref="Surface"/> (immediate, mutable grid) and
/// <see cref="CompositeSurface"/> (deferred, layered composition) to be used
/// interchangeably in compositing operations.
/// </para>
/// <para>
/// Implementations may compute cells lazily (as in <see cref="CompositeSurface"/>)
/// or return pre-stored values (as in <see cref="Surface"/>).
/// </para>
/// </remarks>
public interface ISurfaceSource
{
    /// <summary>
    /// Gets the width of the surface in columns.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the surface in rows.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the cell at the specified position.
    /// </summary>
    /// <remarks>
    /// For <see cref="CompositeSurface"/>, this may trigger lazy resolution
    /// of layers and computed cells.
    /// </remarks>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <returns>The cell at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the position is outside bounds.</exception>
    SurfaceCell GetCell(int x, int y);

    /// <summary>
    /// Tries to get the cell at the specified position.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <param name="cell">The cell at the position, or default if out of bounds.</param>
    /// <returns>True if the position is valid, false otherwise.</returns>
    bool TryGetCell(int x, int y, out SurfaceCell cell);

    /// <summary>
    /// Checks if the specified position is within bounds.
    /// </summary>
    /// <param name="x">The column (0-based).</param>
    /// <param name="y">The row (0-based).</param>
    /// <returns>True if the position is within bounds, false otherwise.</returns>
    bool IsInBounds(int x, int y);
}
