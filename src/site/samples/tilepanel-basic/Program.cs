using Hex1b;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

var state = new MapState();
var dataSource = new GridTileDataSource();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text($"Camera: ({state.CameraX:F1}, {state.CameraY:F1})  Zoom: {state.ZoomLevel}"),
        v.TilePanel(dataSource, state.CameraX, state.CameraY, state.ZoomLevel)
            .OnPan(e =>
            {
                state.CameraX += e.DeltaX;
                state.CameraY += e.DeltaY;
            })
            .OnZoom(e => state.ZoomLevel = e.NewZoomLevel)
    ]))
    .Build();

await terminal.RunAsync();

class MapState
{
    public double CameraX { get; set; }
    public double CameraY { get; set; }
    public int ZoomLevel { get; set; }
}

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
                    isEven ? Hex1bColor.FromRgb(100, 180, 255) : Hex1bColor.FromRgb(180, 180, 180),
                    isEven ? Hex1bColor.FromRgb(20, 40, 80) : Hex1bColor.FromRgb(30, 50, 30));
            }
        }
        return ValueTask.FromResult(tiles);
    }

    static string FormatCoord(int x, int y)
    {
        var s = $"{x},{y}";
        return s.Length <= 3 ? s.PadRight(3) : s[..3];
    }
}
