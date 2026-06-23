using Hex1b.Scene.Geometry;

namespace AsciiEarth;

/// <summary>
/// Builds the high-detail sphere-cap mesh that overlays the base globe, sized to a published
/// <see cref="EarthView.Window"/> so its texels line up exactly with the assembled tile block.
/// </summary>
/// <remarks>
/// The cap is parameterised in the window's tile space: u/v run linearly across the
/// <see cref="EarthView.TilesPerAxis"/> tiles (matching the texture), while latitude is recovered
/// from the Mercator tile-Y so the imagery is not vertically stretched. Positions use the same
/// longitude/latitude→XYZ convention as <see cref="SphereGeometry"/>, at a slightly larger radius
/// so the patch wins the depth test against the base globe.
/// <para>
/// Each vertex's geographic offset from (<c>centerLat</c>, <c>centerLon</c>) is scaled by
/// <c>magnification</c>: at <c>1</c> the cap matches the real tile block and aligns with the globe;
/// for deep zoom a factor &gt; 1 enlarges the (now tiny) window about the facing point so the patch
/// keeps filling the view while its texture still shows finer geography — a magnifier.
/// </para>
/// </remarks>
internal static class DetailPatchGeometry
{
    public static SceneBufferGeometry Create(
        EarthView.Window window,
        double centerLat,
        double centerLon,
        double magnification,
        float radius,
        int segments)
    {
        segments = Math.Max(2, segments);
        var cols = segments + 1;
        var rows = segments + 1;
        var vertexCount = cols * rows;

        var positions = new float[vertexCount * 3];
        var normals = new float[vertexCount * 3];
        var uvs = new float[vertexCount * 2];

        var vi = 0;
        for (var iy = 0; iy < rows; iy++)
        {
            var fy = (float)iy / segments;                       // 0 = north edge, 1 = south edge
            var tileY = window.MinTileY + fy * EarthView.TilesPerAxis;
            var lat = TileCoordinates.TileToLatLon(0, tileY, window.Zoom).Lat;
            var scaledLat = centerLat + (lat - centerLat) * magnification;
            var latRad = scaledLat * Math.PI / 180.0;
            var cosLat = (float)Math.Cos(latRad);
            var sinLat = (float)Math.Sin(latRad);

            for (var ix = 0; ix < cols; ix++)
            {
                var fx = (float)ix / segments;                   // 0 = west edge, 1 = east edge
                var tileX = window.MinTileX + fx * EarthView.TilesPerAxis;
                var lon = TileCoordinates.TileToLatLon(tileX, 0, window.Zoom).Lon;
                var scaledLon = centerLon + NormalizeLonDelta(lon - centerLon) * magnification;
                var lonRad = scaledLon * Math.PI / 180.0;
                var cosLon = (float)Math.Cos(lonRad);
                var sinLon = (float)Math.Sin(lonRad);

                var nx = cosLat * sinLon;
                var ny = sinLat;
                var nz = cosLat * cosLon;

                positions[vi * 3 + 0] = nx * radius;
                positions[vi * 3 + 1] = ny * radius;
                positions[vi * 3 + 2] = nz * radius;

                normals[vi * 3 + 0] = nx;
                normals[vi * 3 + 1] = ny;
                normals[vi * 3 + 2] = nz;

                uvs[vi * 2 + 0] = fx;
                uvs[vi * 2 + 1] = fy;

                vi++;
            }
        }

        var indices = new List<uint>(segments * segments * 6);
        for (var iy = 0; iy < segments; iy++)
        {
            for (var ix = 0; ix < segments; ix++)
            {
                var a = (uint)(iy * cols + ix);
                var b = (uint)((iy + 1) * cols + ix);
                var c = (uint)(iy * cols + ix + 1);
                var d = (uint)((iy + 1) * cols + ix + 1);

                // Same winding as SphereGeometry so face normals point outward (lighting matches).
                indices.Add(a);
                indices.Add(b);
                indices.Add(d);

                indices.Add(a);
                indices.Add(d);
                indices.Add(c);
            }
        }

        var geometry = new SceneBufferGeometry();
        geometry.SetAttribute("position", new SceneBufferAttribute("position", positions, 3));
        geometry.SetAttribute("normal", new SceneBufferAttribute("normal", normals, 3));
        geometry.SetAttribute("uv", new SceneBufferAttribute("uv", uvs, 2));
        geometry.SetIndices(indices.ToArray());

        return geometry;
    }

    // Wraps a longitude difference into [-180, 180] so offsets stay small near the antimeridian
    // before they are magnified about the patch centre.
    private static double NormalizeLonDelta(double delta)
    {
        while (delta > 180.0) delta -= 360.0;
        while (delta < -180.0) delta += 360.0;
        return delta;
    }
}
