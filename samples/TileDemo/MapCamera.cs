namespace TileDemo;

/// <summary>
/// Holds map camera state and provides navigation helpers.
/// Camera position is in lat/lon; character-space coordinates are derived per zoom level.
/// </summary>
/// <remarks>
/// Character space: at OSM zoom Z, the world is (2^Z × 256) chars wide
/// and (2^Z × 128) chars tall (because each char = 1px wide × 2px tall via half-blocks).
/// The TilePanel uses TileSize=(1,1), so TilePanel coordinates = character coordinates.
/// </remarks>
internal sealed class MapCamera
{
    private const int OsmTilePixels = 256;
    private const int CharsPerTileX = OsmTilePixels;     // 256 chars wide per OSM tile
    private const int CharsPerTileY = OsmTilePixels / 2; // 128 chars tall per OSM tile

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>
    /// OSM zoom level (0–18). Higher = more detail.
    /// </summary>
    public int ZoomLevel { get; set; }

    public MapCamera(double latitude, double longitude, int zoomLevel)
    {
        Latitude = latitude;
        Longitude = longitude;
        ZoomLevel = Math.Clamp(zoomLevel, 0, 18);
    }

    /// <summary>
    /// Returns the camera center in character-space coordinates.
    /// </summary>
    public (double X, double Y) CharCenter
    {
        get
        {
            var (tileX, tileY) = TileCoordinates.LatLonToTile(Latitude, Longitude, ZoomLevel);
            return (tileX * CharsPerTileX, tileY * CharsPerTileY);
        }
    }

    /// <summary>
    /// Pans the camera by a delta in character-space units.
    /// </summary>
    public void Pan(double charDeltaX, double charDeltaY)
    {
        var (cx, cy) = CharCenter;
        var newTileX = (cx + charDeltaX) / CharsPerTileX;
        var newTileY = (cy + charDeltaY) / CharsPerTileY;
        var (newLat, newLon) = TileCoordinates.TileToLatLon(newTileX, newTileY, ZoomLevel);
        Latitude = Math.Clamp(newLat, -85.05, 85.05);
        Longitude = ((newLon + 180) % 360 + 360) % 360 - 180;
    }

    /// <summary>
    /// Adjusts zoom level, clamping to valid OSM range.
    /// </summary>
    public void Zoom(int delta)
    {
        ZoomLevel = Math.Clamp(ZoomLevel + delta, 0, 18);
    }
}
