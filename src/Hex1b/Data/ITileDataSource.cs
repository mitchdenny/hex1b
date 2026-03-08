using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Data;

/// <summary>
/// Represents a single tile's visual data — the content and colors to render.
/// </summary>
/// <param name="Content">The text content to render (may be multi-character for larger tiles).</param>
/// <param name="Foreground">The foreground color for the tile.</param>
/// <param name="Background">The background color for the tile.</param>
public readonly record struct TileData(
    string Content,
    Hex1bColor Foreground,
    Hex1bColor Background);

/// <summary>
/// Provides tile data for a <see cref="Hex1b.Widgets.TilePanelWidget"/>.
/// Implementations supply tile dimensions and asynchronously fetch tile content
/// for rectangular regions of the tile grid.
/// </summary>
/// <remarks>
/// The data source represents an infinite or large tile grid. The TilePanel
/// requests only the tiles visible in the current viewport. Tile coordinates
/// can be any integer value (positive or negative), enabling infinite panning.
/// </remarks>
public interface ITileDataSource
{
    /// <summary>
    /// Gets the size of each tile in characters at zoom level 0.
    /// For example, a <c>Size(3, 1)</c> means each tile is 3 characters wide and 1 character tall.
    /// </summary>
    Size TileSize { get; }

    /// <summary>
    /// Fetches tile data for a rectangular region of the tile grid.
    /// </summary>
    /// <param name="tileX">The X coordinate of the top-left tile in the region.</param>
    /// <param name="tileY">The Y coordinate of the top-left tile in the region.</param>
    /// <param name="tilesWide">The number of tiles wide to fetch.</param>
    /// <param name="tilesTall">The number of tiles tall to fetch.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A 2D array of tile data where <c>[x, y]</c> corresponds to tile at
    /// <c>(tileX + x, tileY + y)</c>. The array dimensions must be
    /// <c>[tilesWide, tilesTall]</c>.
    /// </returns>
    ValueTask<TileData[,]> GetTilesAsync(
        int tileX,
        int tileY,
        int tilesWide,
        int tilesTall,
        CancellationToken cancellationToken = default);
}
