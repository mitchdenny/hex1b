using Hex1b;
using Hex1b.Data;
using Hex1b.Widgets;
using TileDemo;

// Camera starts centered on Manhattan, zoom 12 for street-level detail
var camera = new MapCamera(latitude: 40.7484, longitude: -73.9857, zoomLevel: 12);

// Tile infrastructure
using var tileClient = new RasterTileClient();
var dataSource = new OsmTileDataSource(tileClient, camera);

// Landmarks as points of interest
var pois = CreateLandmarks(camera);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((app, options) => ctx =>
    {
        var (cx, cy) = camera.CharCenter;
        return ctx.VStack(v =>
        [
            v.HStack(h =>
            [
                h.Text($" ({camera.Latitude:F4}, {camera.Longitude:F4})  z{camera.ZoomLevel}  "),
                h.Text("↑↓←→ pan  +/- zoom  "),
                h.Text("© OpenStreetMap contributors"),
            ]),
            v.TilePanel(dataSource, cx, cy, 0) // zoom=0: TilePanel doesn't scale, OSM zoom handles detail
                .WithPointsOfInterest(pois)
                .OnPan(e =>
                {
                    camera.Pan(e.DeltaX, e.DeltaY);
                })
                .OnZoom(e =>
                {
                    camera.Zoom(e.Delta);
                    dataSource.ClearDecodedCache();
                    pois = CreateLandmarks(camera);
                }),
        ]);
    })
    .Build();

await terminal.RunAsync();

/// <summary>
/// Creates POIs for well-known landmarks, converting lat/lon to character coordinates.
/// </summary>
static List<TilePointOfInterest> CreateLandmarks(MapCamera camera)
{
    (string Icon, string Label, double Lat, double Lon)[] landmarks =
    [
        ("🗽", "Statue of Liberty", 40.6892, -74.0445),
        ("🏙️", "Empire State", 40.7484, -73.9857),
        ("🌉", "Brooklyn Bridge", 40.7061, -73.9969),
        ("🌳", "Central Park", 40.7829, -73.9654),
        ("⭐", "Times Square", 40.7580, -73.9855),
    ];

    var pois = new List<TilePointOfInterest>();
    foreach (var (icon, label, lat, lon) in landmarks)
    {
        var (tileX, tileY) = TileCoordinates.LatLonToTile(lat, lon, camera.ZoomLevel);
        var charX = tileX * 256;
        var charY = tileY * 128;
        pois.Add(new TilePointOfInterest(charX, charY, icon, label));
    }

    return pois;
}
