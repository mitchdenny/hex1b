using System.Numerics;

namespace ModelViewerDemo;

/// <summary>
/// Generates procedural meshes and loads the bundled Utah Teapot.
/// </summary>
internal static class MeshGenerator
{
    /// <summary>
    /// All built-in model names in display order.
    /// </summary>
    public static readonly string[] ModelNames =
    [
        // Human figures (Quaternius CC0, ~500 faces)
        "male standing", "male running", "male walking", "male sitting",
        "female standing", "female running",
        // Classic CG test models
        "teapot", "suzanne", "beetle", "cow", "spot", "alligator",
        // Miscellaneous OBJ models
        "head", "goblet", "helix", "seashell", "epcot", "space shuttle",
        // Procedural spaceships
        "fighter", "cruiser", "station",
        // Primitives
        "cube", "sphere", "torus", "cylinder", "pyramid",
        "dodecahedron", "icosahedron", "octahedron",
    ];

    private static readonly Dictionary<string, string> EmbeddedModelMap = new()
    {
        ["male standing"] = "male_standing.obj",
        ["male running"] = "male_running.obj",
        ["male walking"] = "male_walking.obj",
        ["male sitting"] = "male_sitting.obj",
        ["female standing"] = "female_standing.obj",
        ["female running"] = "female_running.obj",
        ["teapot"] = "teapot.obj",
        ["suzanne"] = "suzanne.obj",
        ["beetle"] = "beetle.obj",
        ["cow"] = "cow.obj",
        ["spot"] = "spot.obj",
        ["alligator"] = "alligator.obj",
        ["head"] = "head.obj",
        ["goblet"] = "goblet.obj",
        ["helix"] = "helix2.obj",
        ["seashell"] = "seashell.obj",
        ["epcot"] = "epcot.obj",
        ["space shuttle"] = "space_shuttle.obj",
    };

    public static Mesh Create(string name)
    {
        if (EmbeddedModelMap.TryGetValue(name, out var filename))
            return LoadEmbedded(filename);

        return name switch
        {
            "cube" => CreateCube(),
            "sphere" => CreateSphere(),
            "torus" => CreateTorus(),
            "cylinder" => CreateCylinder(),
            "pyramid" => CreatePyramid(),
            "dodecahedron" => CreateDodecahedron(),
            "icosahedron" => CreateIcosahedron(),
            "octahedron" => CreateOctahedron(),
            "fighter" => CreateFighter(),
            "cruiser" => CreateCruiser(),
            "station" => CreateStation(),
            _ => throw new ArgumentException($"Unknown model: {name}"),
        };
    }

    private static Mesh LoadEmbedded(string filename)
    {
        var assembly = typeof(MeshGenerator).Assembly;
        var resourceName = $"ModelViewerDemo.Models.{filename}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Available: " +
                string.Join(", ", assembly.GetManifestResourceNames()));
        return ObjParser.Parse(stream).Normalize();
    }

    public static Mesh CreateCube()
    {
        Vector3[] vertices =
        [
            new(-1, -1, -1), new( 1, -1, -1), new( 1,  1, -1), new(-1,  1, -1),
            new(-1, -1,  1), new( 1, -1,  1), new( 1,  1,  1), new(-1,  1,  1),
        ];

        Face[] faces =
        [
            new(0, 1, 2), new(0, 2, 3),
            new(5, 4, 7), new(5, 7, 6),
            new(4, 0, 3), new(4, 3, 7),
            new(1, 5, 6), new(1, 6, 2),
            new(3, 2, 6), new(3, 6, 7),
            new(4, 5, 1), new(4, 1, 0),
        ];

        return new Mesh(vertices, faces);
    }

    public static Mesh CreateSphere(int rings = 12, int segments = 20)
    {
        var vertices = new List<Vector3>();
        var faces = new List<Face>();

        // Top pole
        vertices.Add(new Vector3(0, 1, 0));

        for (int ring = 1; ring < rings; ring++)
        {
            float phi = MathF.PI * ring / rings;
            float y = MathF.Cos(phi);
            float r = MathF.Sin(phi);
            for (int seg = 0; seg < segments; seg++)
            {
                float theta = 2 * MathF.PI * seg / segments;
                vertices.Add(new Vector3(r * MathF.Cos(theta), y, r * MathF.Sin(theta)));
            }
        }

        // Bottom pole
        vertices.Add(new Vector3(0, -1, 0));

        // Top cap
        for (int seg = 0; seg < segments; seg++)
        {
            int next = (seg + 1) % segments;
            faces.Add(new Face(0, 1 + seg, 1 + next));
        }

        // Middle bands
        for (int ring = 0; ring < rings - 2; ring++)
        {
            int ringStart = 1 + ring * segments;
            int nextRingStart = 1 + (ring + 1) * segments;
            for (int seg = 0; seg < segments; seg++)
            {
                int next = (seg + 1) % segments;
                faces.Add(new Face(ringStart + seg, nextRingStart + seg, nextRingStart + next));
                faces.Add(new Face(ringStart + seg, nextRingStart + next, ringStart + next));
            }
        }

        // Bottom cap
        int bottomPole = vertices.Count - 1;
        int lastRing = 1 + (rings - 2) * segments;
        for (int seg = 0; seg < segments; seg++)
        {
            int next = (seg + 1) % segments;
            faces.Add(new Face(bottomPole, lastRing + next, lastRing + seg));
        }

        return new Mesh([.. vertices], [.. faces]).Normalize();
    }

    public static Mesh CreateTorus(int ringSegments = 20, int tubeSegments = 12,
        float majorRadius = 0.7f, float minorRadius = 0.3f)
    {
        var vertices = new List<Vector3>();
        var faces = new List<Face>();

        for (int i = 0; i < ringSegments; i++)
        {
            float theta = 2 * MathF.PI * i / ringSegments;
            float cx = majorRadius * MathF.Cos(theta);
            float cz = majorRadius * MathF.Sin(theta);

            for (int j = 0; j < tubeSegments; j++)
            {
                float phi = 2 * MathF.PI * j / tubeSegments;
                float x = cx + minorRadius * MathF.Cos(phi) * MathF.Cos(theta);
                float z = cz + minorRadius * MathF.Cos(phi) * MathF.Sin(theta);
                float y = minorRadius * MathF.Sin(phi);
                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int i = 0; i < ringSegments; i++)
        {
            int nextI = (i + 1) % ringSegments;
            for (int j = 0; j < tubeSegments; j++)
            {
                int nextJ = (j + 1) % tubeSegments;
                int a = i * tubeSegments + j;
                int b = i * tubeSegments + nextJ;
                int c = nextI * tubeSegments + nextJ;
                int d = nextI * tubeSegments + j;
                faces.Add(new Face(a, d, c));
                faces.Add(new Face(a, c, b));
            }
        }

        return new Mesh([.. vertices], [.. faces]).Normalize();
    }

    public static Mesh CreateCylinder(int segments = 20)
    {
        var vertices = new List<Vector3>();
        var faces = new List<Face>();

        // Top center, bottom center
        vertices.Add(new Vector3(0, 1, 0));  // 0
        vertices.Add(new Vector3(0, -1, 0)); // 1

        // Top ring
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * MathF.PI * i / segments;
            vertices.Add(new Vector3(MathF.Cos(angle), 1, MathF.Sin(angle)));
        }

        // Bottom ring
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * MathF.PI * i / segments;
            vertices.Add(new Vector3(MathF.Cos(angle), -1, MathF.Sin(angle)));
        }

        int topRing = 2;
        int botRing = 2 + segments;

        // Top cap
        for (int i = 0; i < segments; i++)
            faces.Add(new Face(0, topRing + i, topRing + (i + 1) % segments));

        // Bottom cap
        for (int i = 0; i < segments; i++)
            faces.Add(new Face(1, botRing + (i + 1) % segments, botRing + i));

        // Side
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            faces.Add(new Face(topRing + i, botRing + i, botRing + next));
            faces.Add(new Face(topRing + i, botRing + next, topRing + next));
        }

        return new Mesh([.. vertices], [.. faces]).Normalize();
    }

    public static Mesh CreatePyramid()
    {
        Vector3[] vertices =
        [
            new( 0,  1,  0),  // apex
            new(-1, -1, -1),  // base corners
            new( 1, -1, -1),
            new( 1, -1,  1),
            new(-1, -1,  1),
        ];

        Face[] faces =
        [
            new(0, 1, 2), new(0, 2, 3), new(0, 3, 4), new(0, 4, 1), // sides
            new(1, 3, 2), new(1, 4, 3), // base
        ];

        return new Mesh(vertices, faces).Normalize();
    }

    public static Mesh CreateDodecahedron()
    {
        float phi = (1f + MathF.Sqrt(5f)) / 2f;
        float invPhi = 1f / phi;

        Vector3[] vertices =
        [
            new(-1, -1, -1), new(-1, -1,  1), new(-1,  1, -1), new(-1,  1,  1),
            new( 1, -1, -1), new( 1, -1,  1), new( 1,  1, -1), new( 1,  1,  1),
            new( 0, -invPhi, -phi), new( 0, -invPhi,  phi),
            new( 0,  invPhi, -phi), new( 0,  invPhi,  phi),
            new(-invPhi, -phi, 0), new(-invPhi,  phi, 0),
            new( invPhi, -phi, 0), new( invPhi,  phi, 0),
            new(-phi, 0, -invPhi), new(-phi, 0,  invPhi),
            new( phi, 0, -invPhi), new( phi, 0,  invPhi),
        ];

        // 12 pentagonal faces (consistent outward winding)
        int[][] pentagons =
        [
            [0, 8, 4, 14, 12],
            [16, 2, 10, 8, 0],
            [0, 12, 1, 17, 16],
            [12, 14, 5, 9, 1],
            [1, 9, 11, 3, 17],
            [13, 15, 6, 10, 2],
            [16, 17, 3, 13, 2],
            [3, 11, 7, 15, 13],
            [4, 8, 10, 6, 18],
            [18, 19, 5, 14, 4],
            [19, 7, 11, 9, 5],
            [6, 15, 7, 19, 18],
        ];

        var faces = new List<Face>();
        foreach (var pent in pentagons)
        {
            for (int i = 1; i < pent.Length - 1; i++)
                faces.Add(new Face(pent[0], pent[i], pent[i + 1]));
        }

        return new Mesh(vertices, [.. faces]).Normalize();
    }

    public static Mesh CreateIcosahedron()
    {
        float phi = (1f + MathF.Sqrt(5f)) / 2f;

        Vector3[] vertices =
        [
            new(-1,  phi, 0), new( 1,  phi, 0), new(-1, -phi, 0), new( 1, -phi, 0),
            new( 0, -1,  phi), new( 0,  1,  phi), new( 0, -1, -phi), new( 0,  1, -phi),
            new( phi, 0, -1), new( phi, 0,  1), new(-phi, 0, -1), new(-phi, 0,  1),
        ];

        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = Vector3.Normalize(vertices[i]);

        Face[] faces =
        [
            new(0, 11, 5), new(0, 5, 1), new(0, 1, 7), new(0, 7, 10), new(0, 10, 11),
            new(1, 5, 9), new(5, 11, 4), new(11, 10, 2), new(10, 7, 6), new(7, 1, 8),
            new(3, 9, 4), new(3, 4, 2), new(3, 2, 6), new(3, 6, 8), new(3, 8, 9),
            new(4, 9, 5), new(2, 4, 11), new(6, 2, 10), new(8, 6, 7), new(9, 8, 1),
        ];

        return new Mesh(vertices, faces).Normalize();
    }

    public static Mesh CreateTeapot() => LoadEmbedded("teapot.obj");

    public static Mesh CreateOctahedron()
    {
        Vector3[] vertices =
        [
            new( 0,  1,  0),
            new( 1,  0,  0),
            new( 0,  0,  1),
            new(-1,  0,  0),
            new( 0,  0, -1),
            new( 0, -1,  0),
        ];

        Face[] faces =
        [
            new(0, 1, 2), new(0, 2, 3), new(0, 3, 4), new(0, 4, 1),
            new(5, 2, 1), new(5, 3, 2), new(5, 4, 3), new(5, 1, 4),
        ];

        return new Mesh(vertices, faces).Normalize();
    }

    /// <summary>
    /// A delta-wing fighter: sleek nose, swept wings, twin tail fins.
    /// </summary>
    public static Mesh CreateFighter()
    {
        var b = new MeshBuilder();

        // Fuselage: a series of rectangular cross-sections connected along Z
        // Each ring is [top-left, top-right, bot-right, bot-left] viewed from front
        float fw = 0.15f, fh = 0.10f;
        float mw = 0.20f, mh = 0.12f;
        float tw = 0.18f, th = 0.10f;

        var nose = new Vector3(0, 0, 1.5f);
        Vector3[] fwd = [new(-fw, fh, 0.8f), new(fw, fh, 0.8f), new(fw, -fh, 0.8f), new(-fw, -fh, 0.8f)];
        Vector3[] mid = [new(-mw, mh, 0f), new(mw, mh, 0f), new(mw, -mh, 0f), new(-mw, -mh, 0f)];
        Vector3[] tail = [new(-tw, th, -1f), new(tw, th, -1f), new(tw, -th, -1f), new(-tw, -th, -1f)];
        var tailEnd = new Vector3(0, 0, -1.2f);

        // Nose cone
        b.AddCone(nose, fwd);
        // Fuselage sections
        b.AddSection(fwd, mid);
        b.AddSection(mid, tail);
        // Tail cone
        b.AddCone(tailEnd, tail, reverse: true);

        // Delta wings — flat quads (top + bottom face)
        var lwt = new Vector3(-1.0f, -0.02f, -0.3f);
        b.AddFlatQuad(fwd[0], mid[0], lwt, fwd[3]);  // left wing top (leading+body)
        b.AddFlatQuad(mid[0], tail[0], lwt, mid[3]);  // left wing top (trailing)
        var rwt = new Vector3(1.0f, -0.02f, -0.3f);
        b.AddFlatQuad(mid[1], fwd[1], fwd[2], rwt);   // right wing top (leading)
        b.AddFlatQuad(tail[1], mid[1], rwt, mid[2]);   // right wing top (trailing)

        // Tail fins — thin vertical triangles (double-sided)
        var lft = new Vector3(-0.12f, 0.5f, -1.0f);
        b.AddDoubleSidedTri(mid[0], tail[0], lft);
        var rft = new Vector3(0.12f, 0.5f, -1.0f);
        b.AddDoubleSidedTri(tail[1], mid[1], rft);

        return b.ToMesh().Normalize();
    }

    /// <summary>
    /// A blocky capital ship / cruiser with bridge tower and engine nacelles.
    /// </summary>
    public static Mesh CreateCruiser()
    {
        var b = new MeshBuilder();

        // Main hull sections
        var bow = new Vector3(0, 0, 2.0f);
        float fw = 0.3f, fh = 0.15f;
        float mw = 0.5f, mhh = 0.2f;
        float rw = 0.45f, rh = 0.25f;
        float sw = 0.4f, sh = 0.2f;

        Vector3[] fwd = [new(-fw, fh, 1.2f), new(fw, fh, 1.2f), new(fw, -fh, 1.2f), new(-fw, -fh, 1.2f)];
        Vector3[] mid = [new(-mw, mhh, 0f), new(mw, mhh, 0f), new(mw, -mhh, 0f), new(-mw, -mhh, 0f)];
        Vector3[] rear = [new(-rw, rh, -1.5f), new(rw, rh, -1.5f), new(rw, -rh, -1.5f), new(-rw, -rh, -1.5f)];
        Vector3[] stern = [new(-sw, sh, -1.8f), new(sw, sh, -1.8f), new(sw, -sh, -1.8f), new(-sw, -sh, -1.8f)];

        b.AddCone(bow, fwd);
        b.AddSection(fwd, mid);
        b.AddSection(mid, rear);
        b.AddSection(rear, stern);
        b.AddCap(stern, reverse: true); // stern flat cap

        // Bridge tower on top
        float bw = 0.2f, bd = 0.3f, bh2 = 0.45f;
        b.AddBox(new Vector3(-bw, mhh, 0.3f), new Vector3(bw, bh2, 0.3f - bd));

        // Engine nacelles
        float eh = 0.12f;
        b.AddBox(new Vector3(-0.72f, -eh, -0.8f), new Vector3(-0.48f, eh, -1.6f));
        b.AddBox(new Vector3(0.48f, -eh, -0.8f), new Vector3(0.72f, eh, -1.6f));

        return b.ToMesh().Normalize();
    }

    /// <summary>
    /// A space station with a central hub and ring.
    /// </summary>
    public static Mesh CreateStation(int ringSegments = 24, int tubeSegments = 6)
    {
        var b = new MeshBuilder();

        // Central hub: cylinder along Y axis
        float hubR = 0.25f, hubH = 0.4f;
        int hubSegs = 12;
        b.AddCylinder(Vector3.Zero, Vector3.UnitY, hubR, hubH * 2, hubSegs);

        // Outer ring (torus)
        float majorR = 0.85f, minorR = 0.08f;
        b.AddTorus(Vector3.Zero, majorR, minorR, ringSegments, tubeSegments);

        // Spokes connecting hub to ring
        float sw = 0.03f;
        float innerR = hubR;
        float outerR = majorR - minorR;
        for (int spoke = 0; spoke < 4; spoke++)
        {
            float angle = 2 * MathF.PI * spoke / 4;
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            var from = new Vector3(innerR * cos, 0, innerR * sin);
            var to = new Vector3(outerR * cos, 0, outerR * sin);
            b.AddBeam(from, to, sw);
        }

        return b.ToMesh().Normalize();
    }
}

/// <summary>
/// Helper for building meshes with guaranteed correct face winding.
/// All faces are emitted with outward-pointing normals.
/// </summary>
internal sealed class MeshBuilder
{
    private readonly List<Vector3> _verts = [];
    private readonly List<Face> _faces = [];

    public Mesh ToMesh() => new([.. _verts], [.. _faces]);

    private int V(Vector3 v) { int i = _verts.Count; _verts.Add(v); return i; }

    /// <summary>Add a quad with vertices in CCW order when viewed from outside.</summary>
    private void Quad(int a, int b, int c, int d)
    {
        _faces.Add(new Face(a, b, c));
        _faces.Add(new Face(a, c, d));
    }

    /// <summary>Connect a tip point to a ring of 4 vertices (cone/pyramid).</summary>
    public void AddCone(Vector3 tip, Vector3[] ring, bool reverse = false)
    {
        int t = V(tip);
        int[] ri = new int[ring.Length];
        for (int i = 0; i < ring.Length; i++) ri[i] = V(ring[i]);

        // Compute which winding makes the normal point away from ring centroid
        var centroid = Vector3.Zero;
        foreach (var v in ring) centroid += v;
        centroid /= ring.Length;
        var outDir = tip - centroid;

        for (int i = 0; i < ring.Length; i++)
        {
            int next = (i + 1) % ring.Length;
            // Test winding: normal of (tip, ri, ri+1)
            var normal = Vector3.Cross(ring[i] - tip, ring[next] - tip);
            bool correctWinding = Vector3.Dot(normal, outDir) > 0;
            if (reverse) correctWinding = !correctWinding;

            if (correctWinding)
                _faces.Add(new Face(t, ri[i], ri[next]));
            else
                _faces.Add(new Face(t, ri[next], ri[i]));
        }
    }

    /// <summary>Connect two rings of 4 vertices (fuselage section).</summary>
    public void AddSection(Vector3[] front, Vector3[] back)
    {
        int n = front.Length;
        int[] fi = new int[n], bi = new int[n];
        for (int i = 0; i < n; i++) { fi[i] = V(front[i]); bi[i] = V(back[i]); }

        // Each side panel: front[i], front[i+1], back[i+1], back[i]
        // Normal should point outward from the section center axis
        var center = Vector3.Zero;
        foreach (var v in front) center += v;
        foreach (var v in back) center += v;
        center /= (n * 2);

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            var panelCenter = (front[i] + front[next] + back[next] + back[i]) * 0.25f;
            var outDir = panelCenter - center;
            var normal = Vector3.Cross(front[next] - front[i], back[i] - front[i]);

            if (Vector3.Dot(normal, outDir) > 0)
                Quad(fi[i], fi[next], bi[next], bi[i]);
            else
                Quad(fi[i], bi[i], bi[next], fi[next]);
        }
    }

    /// <summary>Cap a ring of vertices with a fan.</summary>
    public void AddCap(Vector3[] ring, bool reverse = false)
    {
        var center = Vector3.Zero;
        foreach (var v in ring) center += v;
        center /= ring.Length;
        int ci = V(center);
        int[] ri = new int[ring.Length];
        for (int i = 0; i < ring.Length; i++) ri[i] = V(ring[i]);

        // First triangle to determine winding
        var normal = Vector3.Cross(ring[0] - center, ring[1] - center);
        // For reverse caps (stern), we want normal pointing in -Z roughly
        // Check against the direction from section interior to cap
        bool flip = reverse;

        for (int i = 0; i < ring.Length; i++)
        {
            int next = (i + 1) % ring.Length;
            if (flip)
                _faces.Add(new Face(ci, ri[next], ri[i]));
            else
                _faces.Add(new Face(ci, ri[i], ri[next]));
        }
    }

    /// <summary>Add a flat quad (double-sided for wings).</summary>
    public void AddFlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int ai = V(a), bi = V(b), ci = V(c), di = V(d);
        // Both sides
        Quad(ai, bi, ci, di);
        Quad(ai, di, ci, bi);
    }

    /// <summary>Add a double-sided triangle (for fins).</summary>
    public void AddDoubleSidedTri(Vector3 a, Vector3 b, Vector3 c)
    {
        int ai = V(a), bi = V(b), ci = V(c);
        _faces.Add(new Face(ai, bi, ci));
        _faces.Add(new Face(ai, ci, bi));
    }

    /// <summary>Add an axis-aligned box with correct outward normals.</summary>
    public void AddBox(Vector3 min, Vector3 max)
    {
        var mn = Vector3.Min(min, max);
        var mx = Vector3.Max(min, max);

        // 8 vertices
        int v0 = V(new(mn.X, mn.Y, mn.Z));
        int v1 = V(new(mx.X, mn.Y, mn.Z));
        int v2 = V(new(mx.X, mx.Y, mn.Z));
        int v3 = V(new(mn.X, mx.Y, mn.Z));
        int v4 = V(new(mn.X, mn.Y, mx.Z));
        int v5 = V(new(mx.X, mn.Y, mx.Z));
        int v6 = V(new(mx.X, mx.Y, mx.Z));
        int v7 = V(new(mn.X, mx.Y, mx.Z));

        // 6 faces (CCW when viewed from outside)
        Quad(v3, v2, v1, v0); // -Z face
        Quad(v4, v5, v6, v7); // +Z face
        Quad(v0, v1, v5, v4); // -Y face
        Quad(v7, v6, v2, v3); // +Y face
        Quad(v0, v4, v7, v3); // -X face
        Quad(v1, v2, v6, v5); // +X face
    }

    /// <summary>Add a cylinder along an axis direction.</summary>
    public void AddCylinder(Vector3 center, Vector3 axis, float radius, float height, int segments)
    {
        var dir = Vector3.Normalize(axis);
        var top = center + dir * (height / 2);
        var bot = center - dir * (height / 2);

        // Find perpendicular vectors
        var perp1 = MathF.Abs(Vector3.Dot(dir, Vector3.UnitX)) < 0.9f
            ? Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitX))
            : Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitZ));
        var perp2 = Vector3.Cross(dir, perp1);

        int topCenter = V(top);
        int botCenter = V(bot);

        int topStart = _verts.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * MathF.PI * i / segments;
            var offset = radius * (MathF.Cos(angle) * perp1 + MathF.Sin(angle) * perp2);
            V(top + offset);
        }

        int botStart = _verts.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * MathF.PI * i / segments;
            var offset = radius * (MathF.Cos(angle) * perp1 + MathF.Sin(angle) * perp2);
            V(bot + offset);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            // Top cap (normal = +dir)
            _faces.Add(new Face(topCenter, topStart + i, topStart + next));
            // Bottom cap (normal = -dir)
            _faces.Add(new Face(botCenter, botStart + next, botStart + i));
            // Side (normal = outward)
            Quad(topStart + i, topStart + next, botStart + next, botStart + i);
        }
    }

    /// <summary>Add a torus centered at a point in the XZ plane.</summary>
    public void AddTorus(Vector3 center, float majorR, float minorR, int ringSegs, int tubeSegs)
    {
        int start = _verts.Count;

        for (int i = 0; i < ringSegs; i++)
        {
            float theta = 2 * MathF.PI * i / ringSegs;
            float cx = center.X + majorR * MathF.Cos(theta);
            float cz = center.Z + majorR * MathF.Sin(theta);

            for (int j = 0; j < tubeSegs; j++)
            {
                float phi = 2 * MathF.PI * j / tubeSegs;
                float x = cx + minorR * MathF.Cos(phi) * MathF.Cos(theta);
                float z = cz + minorR * MathF.Cos(phi) * MathF.Sin(theta);
                float y = center.Y + minorR * MathF.Sin(phi);
                V(new Vector3(x, y, z));
            }
        }

        for (int i = 0; i < ringSegs; i++)
        {
            int nextI = (i + 1) % ringSegs;
            for (int j = 0; j < tubeSegs; j++)
            {
                int nextJ = (j + 1) % tubeSegs;
                int a = start + i * tubeSegs + j;
                int b = start + i * tubeSegs + nextJ;
                int c = start + nextI * tubeSegs + nextJ;
                int d = start + nextI * tubeSegs + j;

                // Normal should point away from the ring center for this tube segment
                var ringCenter = new Vector3(
                    center.X + majorR * MathF.Cos(2 * MathF.PI * i / ringSegs),
                    center.Y,
                    center.Z + majorR * MathF.Sin(2 * MathF.PI * i / ringSegs));
                var vertPos = _verts[a];
                var outDir = vertPos - ringCenter;
                var normal = Vector3.Cross(_verts[b] - _verts[a], _verts[d] - _verts[a]);

                if (Vector3.Dot(normal, outDir) > 0)
                    Quad(a, b, c, d);
                else
                    Quad(a, d, c, b);
            }
        }
    }

    /// <summary>Add a thin rectangular beam between two points.</summary>
    public void AddBeam(Vector3 from, Vector3 to, float halfWidth)
    {
        var dir = Vector3.Normalize(to - from);
        var up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitY)) < 0.9f
            ? Vector3.UnitY
            : Vector3.UnitX;
        var right = Vector3.Normalize(Vector3.Cross(dir, up));
        up = Vector3.Cross(right, dir);

        var r = right * halfWidth;
        var u = up * halfWidth;

        AddBox(
            Vector3.Min(from - r - u, to - r - u),
            Vector3.Max(from + r + u, to + r + u));
    }
}
