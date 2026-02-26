using System.Globalization;
using System.Numerics;

namespace ModelViewerDemo;

/// <summary>
/// Minimal Wavefront .OBJ parser. Handles v (vertices) and f (faces) lines.
/// Faces are triangulated if they have more than 3 vertices.
/// </summary>
internal static class ObjParser
{
    public static Mesh Parse(Stream stream)
    {
        var vertices = new List<Vector3>();
        var faces = new List<Face>();

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                ParseVertex(line, vertices);
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                ParseFace(line, faces, vertices.Count);
            }
            // Skip vn, vt, mtllib, usemtl, o, g, s, etc.
        }

        return new Mesh([.. vertices], [.. faces]);
    }

    public static Mesh ParseFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    private static void ParseVertex(string line, List<Vector3> vertices)
    {
        // "v x y z [w]"
        var parts = line.AsSpan(2).Trim();
        Span<Range> ranges = stackalloc Range[4];
        int count = parts.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries);
        if (count < 3) return;

        if (float.TryParse(parts[ranges[0]], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            float.TryParse(parts[ranges[1]], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
            float.TryParse(parts[ranges[2]], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
        {
            vertices.Add(new Vector3(x, y, z));
        }
    }

    private static void ParseFace(string line, List<Face> faces, int vertexCount)
    {
        // "f v1 v2 v3 ..." or "f v1/vt1 v2/vt2 ..." or "f v1/vt1/vn1 ..."
        var parts = line.AsSpan(2).Trim();
        Span<Range> ranges = stackalloc Range[16];
        int count = parts.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries);
        if (count < 3) return;

        Span<int> indices = stackalloc int[count];
        for (int i = 0; i < count; i++)
        {
            var token = parts[ranges[i]];
            // Take only the vertex index (before first '/')
            int slashPos = token.IndexOf('/');
            var vertexPart = slashPos >= 0 ? token[..slashPos] : token;

            if (!int.TryParse(vertexPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                return;

            // OBJ uses 1-based indexing; negative indices are relative to end
            indices[i] = idx > 0 ? idx - 1 : vertexCount + idx;
        }

        // Fan triangulation for polygons with > 3 vertices
        for (int i = 1; i < count - 1; i++)
        {
            faces.Add(new Face(indices[0], indices[i], indices[i + 1]));
        }
    }
}
