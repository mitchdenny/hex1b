using Hex1b;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;

/// <summary>
/// A simple tile data source that renders a colored coordinate grid.
/// Each tile shows its (x,y) coordinate as a 3-character string.
/// </summary>
class GridTileDataSource : ITileDataSource
{
    public Size TileSize => new(3, 1);

    public ValueTask<TileData[,]> GetTilesAsync(
        int tileX, int tileY, int tilesWide, int tilesTall,
        CancellationToken cancellationToken = default)
    {
        var tiles = new TileData[tilesWide, tilesTall];
        for (int y = 0; y < tilesTall; y++)
        {
            for (int x = 0; x < tilesWide; x++)
            {
                var tx = tileX + x;
                var ty = tileY + y;
                var isEven = (tx + ty) % 2 == 0;

                tiles[x, y] = new TileData(
                    FormatCoord(tx, ty),
                    isEven ? Hex1bColor.FromRgb(100, 180, 255)
                           : Hex1bColor.FromRgb(180, 180, 180),
                    isEven ? Hex1bColor.FromRgb(20, 40, 80)
                           : Hex1bColor.FromRgb(30, 50, 30));
            }
        }
        return ValueTask.FromResult(tiles);
    }

    private static string FormatCoord(int x, int y)
    {
        var s = $"{x},{y}";
        return s.Length <= 3 ? s.PadRight(3) : s[..3];
    }
}
