namespace Hex1b.Surfaces;

/// <summary>
/// Represents the difference between two surface states.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="SurfaceDiff"/> contains the list of cells that have changed between
/// a previous and current surface state. This is used to generate minimal terminal
/// update sequences.
/// </para>
/// <para>
/// Changed cells are stored in row-major order (sorted by Y, then X) for optimal
/// cursor movement during rendering.
/// </para>
/// </remarks>
public sealed class SurfaceDiff
{
    private readonly List<ChangedCell> _changedCells;

    /// <summary>
    /// Gets the list of changed cells.
    /// </summary>
    public IReadOnlyList<ChangedCell> ChangedCells => _changedCells;

    /// <summary>
    /// Gets whether the diff is empty (no changes).
    /// </summary>
    public bool IsEmpty => _changedCells.Count == 0;

    /// <summary>
    /// Gets the number of changed cells.
    /// </summary>
    public int Count => _changedCells.Count;

    /// <summary>
    /// Creates a new surface diff with the specified changed cells.
    /// </summary>
    /// <param name="changedCells">The list of changed cells (will be sorted in-place).</param>
    internal SurfaceDiff(List<ChangedCell> changedCells)
    {
        _changedCells = changedCells;
        
        // Sort by Y, then X for optimal cursor movement
        _changedCells.Sort((a, b) =>
        {
            var yCompare = a.Y.CompareTo(b.Y);
            return yCompare != 0 ? yCompare : a.X.CompareTo(b.X);
        });
    }

    /// <summary>
    /// Creates an empty diff.
    /// </summary>
    public static SurfaceDiff Empty { get; } = new(new List<ChangedCell>());
}

/// <summary>
/// Represents a cell that has changed between two surface states.
/// </summary>
/// <param name="X">The column position (0-based).</param>
/// <param name="Y">The row position (0-based).</param>
/// <param name="Cell">The new cell value at this position.</param>
public readonly record struct ChangedCell(int X, int Y, SurfaceCell Cell);
