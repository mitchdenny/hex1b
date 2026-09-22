namespace Hex1b.Surfaces;

/// <summary>
/// Represents a cell that has changed between two surface states.
/// </summary>
/// <param name="X">The column position (0-based).</param>
/// <param name="Y">The row position (0-based).</param>
/// <param name="Cell">The new cell value at this position.</param>
public readonly record struct ChangedCell(int X, int Y, SurfaceCell Cell);
