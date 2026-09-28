using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>
/// Common interface for terminal snapshot and snapshot regions.
/// Provides core cell access and region extraction.
/// </summary>
/// <remarks>
/// Columns index stored text cells, not scaled display positions. A horizontal subregion
/// retains its source row's rendition: three logical columns on an enlarged row occupy
/// six display columns when exported to a sufficiently wide terminal.
/// </remarks>
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

    /// <summary>Gets the DEC character width and height mode of a row.</summary>
    /// <param name="row">The zero-based row within this region.</param>
    /// <returns>The row rendition, or single width for an out-of-bounds row.</returns>
    LineRendition GetLineRendition(int row) => LineRendition.SingleWidth;

    /// <summary>Gets the number of addressable logical columns in a row.</summary>
    /// <param name="row">The zero-based row within this region.</param>
    /// <returns>The logical column count, excluding inaccessible storage, or zero for an out-of-bounds row.</returns>
    int GetLogicalWidth(int row) => row >= 0 && row < Height ? Width : 0;

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
