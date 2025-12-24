using Hex1b.Layout;

namespace Hex1b.Terminal.Testing;

/// <summary>
/// Common interface for terminal snapshot and snapshot regions.
/// Provides core cell access and region extraction.
/// </summary>
public interface IHex1bTerminalRegion
{
    /// <summary>
    /// The width of this region.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// The height of this region.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the cell at the specified position within this region.
    /// </summary>
    /// <param name="x">X coordinate (0 to Width-1).</param>
    /// <param name="y">Y coordinate (0 to Height-1).</param>
    /// <returns>The terminal cell, or <see cref="TerminalCell.Empty"/> if out of bounds.</returns>
    TerminalCell GetCell(int x, int y);

    /// <summary>
    /// Gets a sub-region with localized coordinates.
    /// </summary>
    /// <param name="bounds">The bounds of the region to extract, relative to this region.</param>
    /// <returns>A region view that translates local coordinates.</returns>
    Hex1bTerminalSnapshotRegion GetRegion(Rect bounds);
}
