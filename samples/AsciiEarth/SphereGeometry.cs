using Hex1b.Scene.Geometry;

namespace AsciiEarth;

/// <summary>
/// Builds a UV-mapped sphere as a <see cref="SceneBufferGeometry"/>, parameterised directly
/// in geographic (longitude/latitude) space so it lines up with an equirectangular Earth texture.
/// </summary>
/// <remarks>
/// Conventions (right-handed, outward-facing):
/// <list type="bullet">
///   <item>Longitude 0°, latitude 0° faces +Z (toward a camera placed on +Z).</item>
///   <item>East (+longitude) increases toward +X (screen right); north (+latitude) toward +Y (up).</item>
///   <item>UV.u = (lon + 180) / 360, UV.v = (90 − lat) / 180 — linear in latitude (equirectangular),
///         with v = 0 at the north pole to match texture row 0.</item>
/// </list>
/// </remarks>
internal static class SphereGeometry
{
    public static SceneBufferGeometry Create(float radius, int longitudeSegments, int latitudeSegments)
    {
        longitudeSegments = Math.Max(3, longitudeSegments);
        latitudeSegments = Math.Max(2, latitudeSegments);

        var cols = longitudeSegments + 1;
        var rows = latitudeSegments + 1;
        var vertexCount = cols * rows;

        var positions = new float[vertexCount * 3];
        var normals = new float[vertexCount * 3];
        var uvs = new float[vertexCount * 2];

        var vi = 0;
        for (var iy = 0; iy < rows; iy++)
        {
            var v = (float)iy / latitudeSegments;     // 0 at north pole, 1 at south pole
            var latDeg = 90.0 - v * 180.0;
            var latRad = latDeg * Math.PI / 180.0;
            var cosLat = (float)Math.Cos(latRad);
            var sinLat = (float)Math.Sin(latRad);

            for (var ix = 0; ix < cols; ix++)
            {
                var u = (float)ix / longitudeSegments; // 0 at -180°, 1 at +180°
                var lonDeg = -180.0 + u * 360.0;
                var lonRad = lonDeg * Math.PI / 180.0;
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

                uvs[vi * 2 + 0] = u;
                uvs[vi * 2 + 1] = v;

                vi++;
            }
        }

        var indices = new List<uint>(longitudeSegments * latitudeSegments * 6);
        for (var iy = 0; iy < latitudeSegments; iy++)
        {
            for (var ix = 0; ix < longitudeSegments; ix++)
            {
                var a = (uint)(iy * cols + ix);
                var b = (uint)((iy + 1) * cols + ix);
                var c = (uint)(iy * cols + ix + 1);
                var d = (uint)((iy + 1) * cols + ix + 1);

                // Two triangles per grid cell, wound so the face normal points outward.
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
}
