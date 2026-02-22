using Hex1b;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

// Camera state
var cameraX = 0.0;
var cameraY = 0.0;
var zoomLevel = 0;
var selectedPoi = (TilePointOfInterest?)null;

// Data source
var dataSource = new CheckerboardTileDataSource();

// Points of interest
var pois = new List<TilePointOfInterest>
{
    new(0, 0, "🏠", "Home"),
    new(5, 3, "📍", "Marker A"),
    new(-3, 7, "⭐", "Star"),
    new(10, -2, "🏔️", "Mountain"),
    new(-8, -5, "🌊", "Ocean"),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v =>
    [
        // HUD bar at top
        v.HStack(h =>
        [
            h.Text($" Camera: ({cameraX:F1}, {cameraY:F1})  Zoom: {zoomLevel} ({Math.Pow(2, zoomLevel):F1}x)  "),
            h.Text(selectedPoi != null ? $"Selected: {selectedPoi.Label ?? selectedPoi.Icon}" : "Arrow keys: Pan  +/-: Zoom  Click POI for info"),
        ]),

        // Tile panel fills the rest
        v.TilePanel(dataSource, cameraX, cameraY, zoomLevel)
            .WithPointsOfInterest(pois)
            .OnPan(e =>
            {
                cameraX += e.DeltaX;
                cameraY += e.DeltaY;
                selectedPoi = null;
            })
            .OnZoom(e =>
            {
                zoomLevel = e.NewZoomLevel;
            })
            .OnPoiClicked(e =>
            {
                selectedPoi = e.PointOfInterest;
            }),
    ]))
    .Build();

await terminal.RunAsync();

/// <summary>
/// A simple checkerboard tile data source for demonstration.
/// Alternates colors and shows tile coordinates as content.
/// </summary>
internal class CheckerboardTileDataSource : ITileDataSource
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

                // Format coordinate string, truncated to tile width
                var coord = $"{tx},{ty}";
                if (coord.Length > 3) coord = coord[..3];
                coord = coord.PadRight(3);

                tiles[x, y] = new TileData(
                    coord,
                    isEven ? Hex1bColor.White : Hex1bColor.Gray,
                    isEven ? Hex1bColor.FromRgb(30, 30, 80) : Hex1bColor.FromRgb(20, 60, 20));
            }
        }
        return ValueTask.FromResult(tiles);
    }
}
