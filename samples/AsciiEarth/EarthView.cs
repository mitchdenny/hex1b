namespace AsciiEarth;

/// <summary>
/// Pure geometry helpers shared by the orbit controller, the detail-texture builder, and the
/// detail-patch mesh. Decides the bounded tile window shown at high zoom and the camera distance
/// that frames it.
/// </summary>
/// <remarks>
/// Deep zoom is delivered with a fixed-size <see cref="TilesX"/>×<see cref="TilesY"/> tile block
/// centred on the point facing the camera, so the number of tiles downloaded stays bounded at
/// <em>every</em> OSM zoom level — only the geographic span shrinks as you zoom. The block is
/// <em>wider than it is tall</em> because the terminal viewport is far wider than the vertical
/// field of view it is framed to (the braille rasterizer doubles columns but quadruples rows, so
/// the perspective aspect is ≈ 1.6), so a square block would leave the base globe showing along the
/// left/right edges and corners — two levels of detail on screen at once.
/// </remarks>
internal static class EarthView
{
    /// <summary>
    /// Half-height, in tiles, of the on-screen detail region (radius 2 ⇒ a 5-tile-tall band). This
    /// sets the geographic scale the magnifier frames to fill the view vertically; it is
    /// intentionally <em>smaller</em> than the loaded block so there is a margin of off-screen tiles
    /// to slide into while panning.
    /// </summary>
    public const int ViewRadius = 2;

    /// <summary>Vertical side length of the on-screen detail region (2·<see cref="ViewRadius"/> + 1).</summary>
    public const int ViewTilesPerAxis = ViewRadius * 2 + 1;

    /// <summary>
    /// Half-width, in tiles, of the tile block downloaded horizontally. Sized to cover a wide
    /// viewport (the braille rasterizer makes the perspective aspect ≈ <c>cols/rows · 0.5</c>, so a
    /// full-screen 16:9 terminal is ≈ 2.0 and the screen's horizontal half-extent is ≈
    /// <see cref="ViewRadius"/>·aspect ≈ 4–5 tiles) plus a panning margin, so the detail patch reaches
    /// past the left/right edges and the leading edge stays well off-screen (prefetched and cached)
    /// while panning. A block half-width of <c>LoadRadiusX + 0.5</c> tiles covers viewports up to an
    /// aspect of about <c>(LoadRadiusX + 0.5)/(ViewRadius + 0.5)</c>; 6 ⇒ 13 tiles wide covers up to
    /// ≈ 2.6 (past full-screen 16:9, into 21:9 ultrawide territory).
    /// </summary>
    public const int LoadRadiusX = 6;

    /// <summary>
    /// Half-height, in tiles, of the tile block downloaded vertically. One tile larger than
    /// <see cref="ViewRadius"/> on each side so the visible band stays covered by cached imagery as
    /// the user pans north/south, and the texture can slide rather than pop.
    /// </summary>
    public const int LoadRadiusY = 3;

    /// <summary>Width of the downloaded tile block in tiles (2·<see cref="LoadRadiusX"/> + 1).</summary>
    public const int TilesX = LoadRadiusX * 2 + 1;

    /// <summary>Height of the downloaded tile block in tiles (2·<see cref="LoadRadiusY"/> + 1).</summary>
    public const int TilesY = LoadRadiusY * 2 + 1;

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
    /// Computes the detail tile block (a <see cref="TilesX"/>×<see cref="TilesY"/> block) centred on
    /// the given lat/lon at <paramref name="zoom"/>, clamped so it stays on the map vertically (it
    /// wraps horizontally). <see cref="Window.AngularRadiusRad"/> reports the radius of the inner
    /// <see cref="ViewRadius"/> region — the on-screen detail scale — not the larger loaded block, so
    /// the camera framing and magnifier are unaffected by the extra margin tiles.
    /// </summary>
    public static Window ComputeWindow(double centerLat, double centerLon, int zoom)
    {
        var n = 1 << zoom;
        var (cxf, cyf) = TileCoordinates.LatLonToTile(centerLat, centerLon, zoom);

        var minTileX = (int)Math.Floor(cxf) - LoadRadiusX; // may be negative; callers wrap modulo n
        var minTileY = (int)Math.Floor(cyf) - LoadRadiusY;
        minTileY = n > TilesY ? Math.Clamp(minTileY, 0, n - TilesY) : 0;

        var north = TileCoordinates.TileToLatLon(0, minTileY, zoom).Lat;
        var south = TileCoordinates.TileToLatLon(0, minTileY + TilesY, zoom).Lat;
        var west = TileCoordinates.TileToLatLon(minTileX, 0, zoom).Lon;
        var east = TileCoordinates.TileToLatLon(minTileX + TilesX, 0, zoom).Lon;

        // The framed/magnified scale is the inner view region, aligned to the centre tile so it is
        // independent of the load-block clamping above (matches the pre-margin framing exactly).
        var innerMinY = (int)Math.Floor(cyf) - ViewRadius;
        var viewNorth = TileCoordinates.TileToLatLon(0, innerMinY, zoom).Lat;
        var viewSouth = TileCoordinates.TileToLatLon(0, innerMinY + ViewTilesPerAxis, zoom).Lat;
        var angularRadius = (viewNorth - viewSouth) * 0.5 * Math.PI / 180.0;

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

    /// <summary>
    /// Inverse of <see cref="FramingDistance"/>: the cap angular radius (radians) a camera at
    /// <paramref name="distance"/> from the unit sphere's centre frames exactly to the edge of its
    /// field of view. Used to pick the fixed on-screen size of the deep-zoom magnifier patch.
    /// </summary>
    /// <remarks>
    /// From d = cosθ + sinθ/tanβ, write tanβ·cosθ + sinθ = √(1+tan²β)·sin(θ+φ) with φ = atan(tanβ);
    /// then d·tanβ = √(1+tan²β)·sin(θ+φ), so θ = asin(d·tanβ/√(1+tan²β)) − φ.
    /// </remarks>
    public static double AngularRadiusForDistance(double distance, float fieldOfView)
    {
        var beta = fieldOfView * 0.5;
        var t = Math.Tan(beta);
        var r = Math.Sqrt(1.0 + t * t);
        var phi = Math.Atan(t);
        var s = Math.Clamp(distance * t / r, -1.0, 1.0);
        return Math.Asin(s) - phi;
    }
}
