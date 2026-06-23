namespace AsciiEarth;

/// <summary>
/// Pure geometry helpers shared by the orbit controller, the detail-texture builder, and the
/// detail-patch mesh. Decides the bounded tile window shown at high zoom and the camera distance
/// that frames it.
/// </summary>
/// <remarks>
/// Deep zoom is delivered with a fixed-size <see cref="TilesPerAxis"/>×<see cref="TilesPerAxis"/>
/// tile block centred on the point facing the camera, so the number of tiles downloaded stays
/// bounded (≤ 25) at <em>every</em> OSM zoom level — only the geographic span shrinks as you zoom.
/// </remarks>
internal static class EarthView
{
    /// <summary>Half-width of the detail tile block, in tiles (radius 2 ⇒ a 5×5 block).</summary>
    public const int TileRadius = 2;

    /// <summary>Side length of the detail tile block (2·<see cref="TileRadius"/> + 1).</summary>
    public const int TilesPerAxis = TileRadius * 2 + 1;

    /// <summary>Pixels per OSM tile edge.</summary>
    public const int TilePixels = 256;

    /// <summary>
    /// A tile-aligned detail window: the block origin/zoom plus its exact geographic bounds and the
    /// angular radius (along a meridian, in radians) of the cap it covers.
    /// </summary>
    public readonly record struct Window(
        int Zoom,
        int MinTileX,
        int MinTileY,
        double North,
        double South,
        double West,
        double East,
        double AngularRadiusRad);

    /// <summary>
    /// Computes the <see cref="TilesPerAxis"/>-square detail tile block centred on the given
    /// lat/lon at <paramref name="zoom"/>, clamped so it stays on the map vertically (it wraps
    /// horizontally).
    /// </summary>
    public static Window ComputeWindow(double centerLat, double centerLon, int zoom)
    {
        var n = 1 << zoom;
        var (cxf, cyf) = TileCoordinates.LatLonToTile(centerLat, centerLon, zoom);

        var minTileX = (int)Math.Floor(cxf) - TileRadius; // may be negative; callers wrap modulo n
        var minTileY = (int)Math.Floor(cyf) - TileRadius;
        minTileY = n > TilesPerAxis ? Math.Clamp(minTileY, 0, n - TilesPerAxis) : 0;

        var north = TileCoordinates.TileToLatLon(0, minTileY, zoom).Lat;
        var south = TileCoordinates.TileToLatLon(0, minTileY + TilesPerAxis, zoom).Lat;
        var west = TileCoordinates.TileToLatLon(minTileX, 0, zoom).Lon;
        var east = TileCoordinates.TileToLatLon(minTileX + TilesPerAxis, 0, zoom).Lon;

        var angularRadius = (north - south) * 0.5 * Math.PI / 180.0;

        return new Window(zoom, minTileX, minTileY, north, south, west, east, angularRadius);
    }

    /// <summary>
    /// Distance for a camera on +Z, looking at the unit sphere's centre, that frames a spherical
    /// cap of angular radius <paramref name="angularRadiusRad"/> to the edge of the field of view.
    /// </summary>
    /// <remarks>
    /// The surface point at angle θ from the sub-camera point is (sinθ, 0, cosθ). For it to sit on
    /// the FOV edge (half-angle β) we need tan β = sinθ / (d − cosθ), i.e.
    /// d = cosθ + sinθ / tan β. Clamped so the camera neither passes through the surface nor flies
    /// off into space.
    /// </remarks>
    public static float FramingDistance(double angularRadiusRad, float fieldOfView, float minDistance, float maxDistance)
    {
        var beta = fieldOfView * 0.5f;
        var theta = (float)angularRadiusRad;
        var d = MathF.Cos(theta) + (MathF.Sin(theta) / MathF.Tan(beta));
        return Math.Clamp(d, minDistance, maxDistance);
    }
}
