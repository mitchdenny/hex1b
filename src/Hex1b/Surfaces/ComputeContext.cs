namespace Hex1b.Surfaces;

/// <summary>
/// Provides context for computing a cell value during layer resolution.
/// </summary>
/// <remarks>
/// <para>
/// This ref struct is passed to computed cell delegates during <see cref="CompositeSurface.Flatten"/>
/// or <see cref="CompositeSurface.GetCell"/>. It provides access to:
/// <list type="bullet">
///   <item>The position being computed</item>
///   <item>Cells from layers below the current layer</item>
///   <item>Adjacent cells on the same layer</item>
/// </list>
/// </para>
/// <para>
/// The context includes cycle detection - if a computed cell queries another cell that is
/// currently being computed (directly or indirectly), a fallback value is returned to
/// prevent infinite recursion.
/// </para>
/// </remarks>
public readonly ref struct ComputeContext
{
    private readonly CompositeSurface.LayerResolutionContext _context;
    private readonly int _currentLayerIndex;

    /// <summary>
    /// Gets the X position (column) of the cell being computed.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y position (row) of the cell being computed.
    /// </summary>
    public int Y { get; }

    internal ComputeContext(int x, int y, int currentLayerIndex, CompositeSurface.LayerResolutionContext context)
    {
        X = x;
        Y = y;
        _currentLayerIndex = currentLayerIndex;
        _context = context;
    }

    /// <summary>
    /// Gets the resolved cell from all layers below the current layer at this position.
    /// </summary>
    /// <remarks>
    /// This composites all layers from 0 up to (but not including) the current layer,
    /// returning what would be visible if the current layer were transparent at this position.
    /// </remarks>
    /// <returns>The composited cell from layers below.</returns>
    public SurfaceCell GetBelow()
    {
        return _context.ResolveCellUpToLayer(X, Y, _currentLayerIndex);
    }

    /// <summary>
    /// Gets the resolved cell from layers below at the specified position.
    /// </summary>
    /// <param name="x">The X position to query.</param>
    /// <param name="y">The Y position to query.</param>
    /// <returns>The composited cell from layers below at the specified position.</returns>
    public SurfaceCell GetBelowAt(int x, int y)
    {
        if (!_context.IsInBounds(x, y))
            return SurfaceCells.Empty;
        
        return _context.ResolveCellUpToLayer(x, y, _currentLayerIndex);
    }

    /// <summary>
    /// Gets a cell from an adjacent position on the same layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This queries the cell at (X + dx, Y + dy) on the current layer.
    /// If that cell is also computed, it will be resolved (with cycle detection).
    /// </para>
    /// <para>
    /// If the adjacent position is out of bounds or would cause a cycle,
    /// <see cref="SurfaceCells.Empty"/> is returned.
    /// </para>
    /// </remarks>
    /// <param name="dx">The X offset from the current position.</param>
    /// <param name="dy">The Y offset from the current position.</param>
    /// <returns>The cell at the adjacent position, or empty if out of bounds or cyclic.</returns>
    public SurfaceCell GetAdjacent(int dx, int dy)
    {
        var targetX = X + dx;
        var targetY = Y + dy;

        if (!_context.IsInBounds(targetX, targetY))
            return SurfaceCells.Empty;

        return _context.ResolveCellAtLayer(targetX, targetY, _currentLayerIndex);
    }

    /// <summary>
    /// Gets a cell from an absolute position on the same layer.
    /// </summary>
    /// <param name="x">The X position to query.</param>
    /// <param name="y">The Y position to query.</param>
    /// <returns>The cell at the specified position, or empty if out of bounds or cyclic.</returns>
    public SurfaceCell GetAtLayer(int x, int y)
    {
        if (!_context.IsInBounds(x, y))
            return SurfaceCells.Empty;

        return _context.ResolveCellAtLayer(x, y, _currentLayerIndex);
    }
}

/// <summary>
/// Delegate for computing a cell value dynamically.
/// </summary>
/// <remarks>
/// <para>
/// Computed cells are evaluated lazily during <see cref="CompositeSurface.Flatten"/> or
/// when accessing cells via <see cref="CompositeSurface.GetCell"/>.
/// </para>
/// <para>
/// The delegate receives a <see cref="ComputeContext"/> that provides access to
/// the cell's position, cells from layers below, and adjacent cells.
/// </para>
/// </remarks>
/// <param name="context">The context providing position and cell access.</param>
/// <returns>The computed cell value.</returns>
public delegate SurfaceCell CellCompute(ComputeContext context);
