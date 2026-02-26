using System.Numerics;

namespace ModelViewerDemo;

/// <summary>
/// An edge connecting two vertices by index.
/// </summary>
internal readonly record struct Edge(int A, int B);

/// <summary>
/// A triangle face defined by three vertex indices.
/// </summary>
internal readonly record struct Face(int A, int B, int C);

/// <summary>
/// A simple 3D mesh: vertices, triangular faces, and edges derived from faces.
/// </summary>
internal sealed class Mesh
{
    public Vector3[] Vertices { get; }
    public Face[] Faces { get; }
    public Edge[] Edges { get; }

    /// <summary>
    /// For each edge, the indices of the (up to 2) adjacent faces.
    /// </summary>
    public int[][] EdgeAdjacentFaces { get; }

    /// <summary>
    /// Precomputed face normals (unnormalized, for back-face culling sign check only).
    /// </summary>
    public Vector3[] FaceNormals { get; }

    public Mesh(Vector3[] vertices, Face[] faces)
    {
        Vertices = vertices;
        Faces = faces;
        FaceNormals = ComputeFaceNormals(vertices, faces);
        (Edges, EdgeAdjacentFaces) = ComputeEdgesAndAdjacency(faces);
    }

    /// <summary>
    /// Returns a new mesh centered at origin and scaled to fit in [-1, 1] on all axes.
    /// </summary>
    public Mesh Normalize()
    {
        if (Vertices.Length == 0) return this;

        var min = Vertices[0];
        var max = Vertices[0];
        foreach (var v in Vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        var center = (min + max) * 0.5f;
        var extent = max - min;
        var scale = 2f / MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z));

        var normalized = new Vector3[Vertices.Length];
        for (int i = 0; i < Vertices.Length; i++)
        {
            normalized[i] = (Vertices[i] - center) * scale;
        }

        return new Mesh(normalized, Faces);
    }

    private static Vector3[] ComputeFaceNormals(Vector3[] vertices, Face[] faces)
    {
        var normals = new Vector3[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            var a = vertices[faces[i].A];
            var b = vertices[faces[i].B];
            var c = vertices[faces[i].C];
            normals[i] = Vector3.Cross(b - a, c - a);
        }
        return normals;
    }

    private static (Edge[], int[][]) ComputeEdgesAndAdjacency(Face[] faces)
    {
        var edgeMap = new Dictionary<(int, int), List<int>>();

        for (int fi = 0; fi < faces.Length; fi++)
        {
            var f = faces[fi];
            AddEdge(edgeMap, f.A, f.B, fi);
            AddEdge(edgeMap, f.B, f.C, fi);
            AddEdge(edgeMap, f.C, f.A, fi);
        }

        var edges = new Edge[edgeMap.Count];
        var adjacency = new int[edgeMap.Count][];
        int idx = 0;
        foreach (var (key, faceList) in edgeMap)
        {
            edges[idx] = new Edge(key.Item1, key.Item2);
            adjacency[idx] = faceList.ToArray();
            idx++;
        }

        return (edges, adjacency);
    }

    private static void AddEdge(Dictionary<(int, int), List<int>> map, int a, int b, int faceIndex)
    {
        var key = a < b ? (a, b) : (b, a);
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }
        list.Add(faceIndex);
    }
}
