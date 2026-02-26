using System.Numerics;

namespace ModelViewerDemo;

/// <summary>
/// Mesh simplification using Quadric Error Metrics (Garland &amp; Heckbert 1997).
/// Iteratively collapses the lowest-cost edge until the target face count is reached.
/// </summary>
internal static class MeshDecimator
{
    /// <summary>
    /// Reduce a mesh to approximately <paramref name="targetFaces"/> faces.
    /// Returns the original mesh if it already has fewer faces than the target.
    /// </summary>
    public static Mesh Decimate(Mesh mesh, int targetFaces)
    {
        if (mesh.Faces.Length <= targetFaces || targetFaces < 4)
            return mesh;

        int vertCount = mesh.Vertices.Length;
        int faceCount = mesh.Faces.Length;

        // Mutable vertex positions
        var positions = new Vector3[vertCount];
        Array.Copy(mesh.Vertices, positions, vertCount);

        // Mutable face list (indices may be updated during collapse)
        var faces = new int[faceCount * 3];
        for (int i = 0; i < faceCount; i++)
        {
            faces[i * 3] = mesh.Faces[i].A;
            faces[i * 3 + 1] = mesh.Faces[i].B;
            faces[i * 3 + 2] = mesh.Faces[i].C;
        }

        // Track which faces are still alive
        var faceAlive = new bool[faceCount];
        Array.Fill(faceAlive, true);
        int aliveFaces = faceCount;

        // Union-find for merged vertices
        var parent = new int[vertCount];
        for (int i = 0; i < vertCount; i++) parent[i] = i;

        // Compute quadrics per vertex
        var quadrics = new Quadric[vertCount];
        for (int fi = 0; fi < faceCount; fi++)
        {
            int a = faces[fi * 3], b = faces[fi * 3 + 1], c = faces[fi * 3 + 2];
            var plane = ComputePlane(positions[a], positions[b], positions[c]);
            var kp = Quadric.FromPlane(plane);
            quadrics[a] = quadrics[a].Add(kp);
            quadrics[b] = quadrics[b].Add(kp);
            quadrics[c] = quadrics[c].Add(kp);
        }

        // Build edge set with costs
        var edgeCosts = new SortedSet<EdgeCost>(EdgeCostComparer.Instance);
        var edgeSet = new HashSet<long>();

        for (int fi = 0; fi < faceCount; fi++)
        {
            int a = faces[fi * 3], b = faces[fi * 3 + 1], c = faces[fi * 3 + 2];
            TryAddEdge(a, b, positions, quadrics, edgeCosts, edgeSet);
            TryAddEdge(b, c, positions, quadrics, edgeCosts, edgeSet);
            TryAddEdge(c, a, positions, quadrics, edgeCosts, edgeSet);
        }

        // Build per-vertex face adjacency for fast updates
        var vertFaces = new List<int>[vertCount];
        for (int i = 0; i < vertCount; i++) vertFaces[i] = [];
        for (int fi = 0; fi < faceCount; fi++)
        {
            vertFaces[faces[fi * 3]].Add(fi);
            vertFaces[faces[fi * 3 + 1]].Add(fi);
            vertFaces[faces[fi * 3 + 2]].Add(fi);
        }

        // Iteratively collapse lowest-cost edge
        while (aliveFaces > targetFaces && edgeCosts.Count > 0)
        {
            var best = edgeCosts.Min!;
            edgeCosts.Remove(best);
            long edgeKey = PackEdge(best.V0, best.V1);
            edgeSet.Remove(edgeKey);

            int keep = Find(parent, best.V0);
            int remove = Find(parent, best.V1);
            if (keep == remove) continue; // already merged

            // Merge: reparent 'remove' → 'keep'
            parent[remove] = keep;
            positions[keep] = best.OptimalPos;
            quadrics[keep] = quadrics[keep].Add(quadrics[remove]);

            // Update all faces referencing 'remove'
            foreach (int fi in vertFaces[remove])
            {
                if (!faceAlive[fi]) continue;

                for (int j = 0; j < 3; j++)
                {
                    if (Find(parent, faces[fi * 3 + j]) == keep ||
                        faces[fi * 3 + j] == remove)
                        faces[fi * 3 + j] = keep;
                }

                // Resolve all indices through union-find
                faces[fi * 3] = Find(parent, faces[fi * 3]);
                faces[fi * 3 + 1] = Find(parent, faces[fi * 3 + 1]);
                faces[fi * 3 + 2] = Find(parent, faces[fi * 3 + 2]);

                // Check for degenerate face
                int fa = faces[fi * 3], fb = faces[fi * 3 + 1], fc = faces[fi * 3 + 2];
                if (fa == fb || fb == fc || fc == fa)
                {
                    faceAlive[fi] = false;
                    aliveFaces--;
                }
            }

            // Transfer face adjacency
            foreach (int fi in vertFaces[remove])
            {
                if (faceAlive[fi] && !vertFaces[keep].Contains(fi))
                    vertFaces[keep].Add(fi);
            }

            // Re-add edges for affected neighbors
            foreach (int fi in vertFaces[keep])
            {
                if (!faceAlive[fi]) continue;
                int a = faces[fi * 3], b = faces[fi * 3 + 1], c = faces[fi * 3 + 2];
                TryAddEdge(Find(parent, a), Find(parent, b), positions, quadrics, edgeCosts, edgeSet);
                TryAddEdge(Find(parent, b), Find(parent, c), positions, quadrics, edgeCosts, edgeSet);
                TryAddEdge(Find(parent, c), Find(parent, a), positions, quadrics, edgeCosts, edgeSet);
            }
        }

        // Compact result
        var vertexMap = new Dictionary<int, int>();
        var newVerts = new List<Vector3>();
        var newFaces = new List<Face>();

        for (int fi = 0; fi < faceCount; fi++)
        {
            if (!faceAlive[fi]) continue;
            int a = Find(parent, faces[fi * 3]);
            int b = Find(parent, faces[fi * 3 + 1]);
            int c = Find(parent, faces[fi * 3 + 2]);
            if (a == b || b == c || c == a) continue;

            int ma = MapVertex(a, vertexMap, newVerts, positions);
            int mb = MapVertex(b, vertexMap, newVerts, positions);
            int mc = MapVertex(c, vertexMap, newVerts, positions);
            newFaces.Add(new Face(ma, mb, mc));
        }

        return new Mesh(newVerts.ToArray(), newFaces.ToArray());
    }

    /// <summary>
    /// Pre-compute a set of LOD meshes at fixed reduction ratios.
    /// Returns an array where index 0 is the original mesh and subsequent
    /// entries have progressively fewer faces.
    /// </summary>
    public static (string Label, Mesh Mesh)[] BuildLodChain(Mesh original)
    {
        var ratios = new[] { 1.0f, 0.75f, 0.50f, 0.30f, 0.15f, 0.07f, 0.03f };
        var results = new List<(string, Mesh)>();

        foreach (float r in ratios)
        {
            int target = Math.Max(4, (int)(original.Faces.Length * r));
            if (target >= original.Faces.Length)
            {
                results.Add(("100%", original));
            }
            else
            {
                var decimated = Decimate(original, target);
                int pct = (int)MathF.Round(100f * decimated.Faces.Length / original.Faces.Length);
                results.Add(($"{pct}%", decimated));
            }
        }

        return results.ToArray();
    }

    private static int MapVertex(int oldIdx, Dictionary<int, int> map,
        List<Vector3> verts, Vector3[] positions)
    {
        if (!map.TryGetValue(oldIdx, out int newIdx))
        {
            newIdx = verts.Count;
            map[oldIdx] = newIdx;
            verts.Add(positions[oldIdx]);
        }
        return newIdx;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]]; // path compression
            i = parent[i];
        }
        return i;
    }

    private static long PackEdge(int a, int b)
    {
        int lo = Math.Min(a, b), hi = Math.Max(a, b);
        return ((long)lo << 32) | (uint)hi;
    }

    private static void TryAddEdge(int a, int b, Vector3[] positions, Quadric[] quadrics,
        SortedSet<EdgeCost> costs, HashSet<long> edgeSet)
    {
        if (a == b) return;
        long key = PackEdge(a, b);
        if (!edgeSet.Add(key)) return;

        var q = quadrics[a].Add(quadrics[b]);
        var (cost, optPos) = q.OptimalVertex(positions[a], positions[b]);
        int lo = Math.Min(a, b), hi = Math.Max(a, b);
        costs.Add(new EdgeCost(lo, hi, cost, optPos));
    }

    private static Vector4 ComputePlane(Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        float len = n.Length();
        if (len < 1e-10f) return Vector4.Zero;
        n /= len;
        return new Vector4(n, -Vector3.Dot(n, a));
    }

    /// <summary>
    /// Symmetric 4×4 quadric matrix stored as 10 unique floats.
    /// Q = [a2  ab  ac  ad]
    ///     [ab  b2  bc  bd]
    ///     [ac  bc  c2  cd]
    ///     [ad  bd  cd  d2]
    /// </summary>
    private readonly record struct Quadric(
        float A2, float AB, float AC, float AD,
        float B2, float BC, float BD,
        float C2, float CD,
        float D2)
    {
        public static Quadric FromPlane(Vector4 p) => new(
            p.X * p.X, p.X * p.Y, p.X * p.Z, p.X * p.W,
            p.Y * p.Y, p.Y * p.Z, p.Y * p.W,
            p.Z * p.Z, p.Z * p.W,
            p.W * p.W);

        public Quadric Add(Quadric o) => new(
            A2 + o.A2, AB + o.AB, AC + o.AC, AD + o.AD,
            B2 + o.B2, BC + o.BC, BD + o.BD,
            C2 + o.C2, CD + o.CD,
            D2 + o.D2);

        public float Evaluate(Vector3 v) =>
            A2 * v.X * v.X + 2 * AB * v.X * v.Y + 2 * AC * v.X * v.Z + 2 * AD * v.X +
            B2 * v.Y * v.Y + 2 * BC * v.Y * v.Z + 2 * BD * v.Y +
            C2 * v.Z * v.Z + 2 * CD * v.Z +
            D2;

        /// <summary>
        /// Find the position that minimizes quadric error. Falls back to
        /// the best of the two endpoints or midpoint if the matrix is singular.
        /// </summary>
        public (float Cost, Vector3 Position) OptimalVertex(Vector3 v1, Vector3 v2)
        {
            // Try to solve the 3×3 linear system for optimal position
            // [A2 AB AC] [x]   [-AD]
            // [AB B2 BC] [y] = [-BD]
            // [AC BC C2] [z]   [-CD]
            float det =
                A2 * (B2 * C2 - BC * BC) -
                AB * (AB * C2 - BC * AC) +
                AC * (AB * BC - B2 * AC);

            if (MathF.Abs(det) > 1e-8f)
            {
                float invDet = 1f / det;
                float x = invDet * ((-AD) * (B2 * C2 - BC * BC) - AB * ((-BD) * C2 - BC * (-CD)) + AC * ((-BD) * BC - B2 * (-CD)));
                float y = invDet * (A2 * ((-BD) * C2 - BC * (-CD)) - (-AD) * (AB * C2 - BC * AC) + AC * (AB * (-CD) - (-BD) * AC));
                float z = invDet * (A2 * (B2 * (-CD) - (-BD) * BC) - AB * (AB * (-CD) - (-BD) * AC) + (-AD) * (AB * BC - B2 * AC));
                var optimal = new Vector3(x, y, z);

                // Only accept if not too far from the edge
                var mid = (v1 + v2) * 0.5f;
                float edgeLen = Vector3.Distance(v1, v2);
                if (Vector3.Distance(optimal, mid) < edgeLen * 3f)
                    return (Evaluate(optimal), optimal);
            }

            // Fallback: pick the best of v1, v2, midpoint
            var m = (v1 + v2) * 0.5f;
            float c1 = Evaluate(v1), c2 = Evaluate(v2), cm = Evaluate(m);
            if (c1 <= c2 && c1 <= cm) return (c1, v1);
            if (c2 <= c1 && c2 <= cm) return (c2, v2);
            return (cm, m);
        }
    }

    private readonly record struct EdgeCost(int V0, int V1, float Cost, Vector3 OptimalPos);

    private sealed class EdgeCostComparer : IComparer<EdgeCost>
    {
        public static readonly EdgeCostComparer Instance = new();

        public int Compare(EdgeCost a, EdgeCost b)
        {
            int c = a.Cost.CompareTo(b.Cost);
            if (c != 0) return c;
            c = a.V0.CompareTo(b.V0);
            if (c != 0) return c;
            return a.V1.CompareTo(b.V1);
        }
    }
}
