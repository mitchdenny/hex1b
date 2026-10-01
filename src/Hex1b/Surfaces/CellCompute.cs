namespace Hex1b.Surfaces;

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
