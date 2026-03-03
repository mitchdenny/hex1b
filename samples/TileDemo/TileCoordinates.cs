namespace TileDemo;

/// <summary>
/// Standard slippy map math for converting between lat/lon and OSM tile coordinates.
/// See: https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
/// </summary>
internal static class TileCoordinates
{
    /// <summary>
    /// Converts latitude/longitude to fractional tile coordinates at a given zoom level.
    /// </summary>
    public static (double X, double Y) LatLonToTile(double lat, double lon, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var x = (lon + 180.0) / 360.0 * n;
        var latRad = lat * Math.PI / 180.0;
        var y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        return (x, y);
    }

    /// <summary>
    /// Converts tile coordinates back to latitude/longitude (north-west corner of the tile).
    /// </summary>
    public static (double Lat, double Lon) TileToLatLon(double tileX, double tileY, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var lon = tileX / n * 360.0 - 180.0;
        var latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * tileY / n)));
        var lat = latRad * 180.0 / Math.PI;
        return (lat, lon);
    }
}
