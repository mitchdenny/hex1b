using System.Numerics;
using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;

namespace Hex1bInsideSpectreTui;

/// <summary>
/// Self-contained port of the GlobeDemo globe so it can run as a Hex1b
/// SurfaceWidget layer rendered into a Spectre.Tui sub-region. Holds all
/// mesh, contour, cloud, and POI state plus the live rotation / zoom /
/// weather-drift state that mutates between frames.
/// </summary>
internal sealed class Globe
{
    private readonly List<Vector3> _vertices;
    private readonly List<(Vector3 a, Vector3 b, double level)> _contourSegments;
    private readonly int _contourLevels;
    private readonly List<(Vector3 a, Vector3 b)> _cloudSegments;
    private readonly bool[] _cloudShadow;
    private readonly Dictionary<(int, int, int), List<int>> _spatialGrid;
    private readonly List<PointOfInterest> _pois;
    private readonly List<(string name, int cx, int cy, int poiIndex)> _poiScreenPositions = new();

    private Quaternion _rotQ = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f);
    private double _zoom = 1.0;
    private double _weatherSeconds;

    /// <summary>
    /// Auto-rotation rate around the Y axis, radians per second. Set to 0 to
    /// stop the spin (e.g. while an interactive control is dragging).
    /// </summary>
    public float AutoYawRadiansPerSecond { get; set; } = 0.18f;

    public Globe()
    {
        var (vertices, triangles, altitudes) = GenerateTopoSphere(subdivisions: 4);
        _vertices = vertices;
        _contourLevels = 18;
        _contourSegments = BuildContourSegments(vertices, triangles, altitudes, _contourLevels);

        var cloudCoverage = new List<double>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            cloudCoverage.Add(Fbm(v.X * 1.3 + 100.7, v.Y * 1.3 + 200.3, v.Z * 1.3 + 300.1, octaves: 4));
        }
        var cMin = cloudCoverage.Min();
        var cMax = cloudCoverage.Max();
        var cRange = cMax - cMin;
        if (cRange < 0.001) cRange = 1;
        for (int i = 0; i < cloudCoverage.Count; i++)
        {
            cloudCoverage[i] = (cloudCoverage[i] - cMin) / cRange;
        }

        const double cloudThreshold = 0.62;
        _cloudSegments = new List<(Vector3 a, Vector3 b)>();
        foreach (var (ia, ib, ic) in triangles)
        {
            double ca = cloudCoverage[ia], cb = cloudCoverage[ib], cc = cloudCoverage[ic];
            var crossings = new List<Vector3>(3);
            TryInterpolateEdge(vertices[ia], vertices[ib], ca, cb, cloudThreshold, crossings);
            TryInterpolateEdge(vertices[ib], vertices[ic], cb, cc, cloudThreshold, crossings);
            TryInterpolateEdge(vertices[ic], vertices[ia], cc, ca, cloudThreshold, crossings);
            if (crossings.Count >= 2)
            {
                _cloudSegments.Add((
                    Vector3.Normalize(crossings[0]) * 1.12f,
                    Vector3.Normalize(crossings[1]) * 1.12f));
            }
        }

        _cloudShadow = new bool[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            _cloudShadow[i] = cloudCoverage[i] > cloudThreshold;
        }

        const int spatialBuckets = 20;
        _spatialGrid = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            var key = ((int)((v.X + 1.5) * spatialBuckets),
                       (int)((v.Y + 1.5) * spatialBuckets),
                       (int)((v.Z + 1.5) * spatialBuckets));
            if (!_spatialGrid.TryGetValue(key, out var list))
            {
                list = new List<int>();
                _spatialGrid[key] = list;
            }
            list.Add(i);
        }

        _pois = new List<PointOfInterest>
        {
            Poi("Arconis Prime",     40.7,   -74.0),
            Poi("Veldra Spire",      51.5,    -0.1),
            Poi("Kythera Station",   35.7,   139.7),
            Poi("Port Solace",      -33.9,   151.2),
            Poi("The Obsidian Gate", 30.0,    31.2),
            Poi("Nova Cascada",     -22.9,   -43.2),
            Poi("Ashenmoor",         19.1,    72.9),
            Poi("Drift Colony 7",   -33.9,    18.4),
            Poi("Cryo Citadel",      55.8,    37.6),
            Poi("Helios Reach",      39.9,   116.4),
            Poi("Neon Abyss",        34.1,  -118.2),
            Poi("Thornhaven",        -1.3,    36.8),
            Poi("Frostlight Keep",   64.1,   -21.9),
            Poi("Undervault",         1.4,   103.8),
        };
    }

    /// <summary>Advance auto-rotation and cloud drift by the given frame time.</summary>
    public void Tick(double deltaSeconds)
    {
        _weatherSeconds += deltaSeconds;
        if (AutoYawRadiansPerSecond != 0f)
        {
            var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, AutoYawRadiansPerSecond * (float)deltaSeconds);
            _rotQ = Quaternion.Normalize(yaw * _rotQ);
        }
    }

    /// <summary>Apply a yaw rotation (left/right). Used by arrow-key handlers.</summary>
    public void Yaw(float radians)
    {
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians);
        _rotQ = Quaternion.Normalize(q * _rotQ);
    }

    /// <summary>Apply a pitch rotation (up/down). Used by arrow-key handlers.</summary>
    public void Pitch(float radians)
    {
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitX, radians);
        _rotQ = Quaternion.Normalize(q * _rotQ);
    }

    /// <summary>Multiply the zoom level. Use values > 1 to zoom in.</summary>
    public void ScaleZoom(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, 0.3, 5.0);
    }

    /// <summary>
    /// Render the globe into the supplied surface. Called from the
    /// SurfaceWidget layer builder on every Spectre.Tui frame.
    /// </summary>
    public void Draw(Surface surface)
    {
        var cloudDrift = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(_weatherSeconds * 0.02));
        DrawGlobe(surface, _contourSegments, _contourLevels, _cloudSegments, _cloudShadow,
            _vertices, _spatialGrid, cloudDrift, _rotQ, _zoom, _pois, _poiScreenPositions);
    }

    // ---------------------------------------------------------------------
    // Below: lightly trimmed copy of the rendering math from
    // samples/GlobeDemo/Program.cs. Kept verbatim for parity.
    // ---------------------------------------------------------------------

    private static List<(Vector3 a, Vector3 b, double level)> BuildContourSegments(
        List<Vector3> verts, List<(int a, int b, int c)> tris, List<double> alts, int levels)
    {
        var segments = new List<(Vector3, Vector3, double)>();
        for (int lvl = 0; lvl < levels; lvl++)
        {
            double threshold = (lvl + 0.5) / levels;
            foreach (var (ia, ib, ic) in tris)
            {
                double aa = alts[ia], ab = alts[ib], ac = alts[ic];
                var crossings = new List<Vector3>(3);
                TryInterpolateEdge(verts[ia], verts[ib], aa, ab, threshold, crossings);
                TryInterpolateEdge(verts[ib], verts[ic], ab, ac, threshold, crossings);
                TryInterpolateEdge(verts[ic], verts[ia], ac, aa, threshold, crossings);
                if (crossings.Count >= 2)
                {
                    double r = 1.0 + threshold * 0.15;
                    var p0 = Vector3.Normalize(crossings[0]) * (float)r;
                    var p1 = Vector3.Normalize(crossings[1]) * (float)r;
                    segments.Add((p0, p1, threshold));
                }
            }
        }
        return segments;
    }

    private static void TryInterpolateEdge(Vector3 va, Vector3 vb, double aa, double ab,
        double threshold, List<Vector3> crossings)
    {
        if ((aa < threshold) == (ab < threshold)) return;
        double t = (threshold - aa) / (ab - aa);
        crossings.Add(Vector3.Lerp(va, vb, (float)t));
    }

    private static void DrawGlobe(Surface surface,
        List<(Vector3 a, Vector3 b, double level)> contourSegments,
        int contourLevels,
        List<(Vector3 a, Vector3 b)> cloudSegments,
        bool[] cloudShadow, List<Vector3> meshVertices,
        Dictionary<(int, int, int), List<int>> spatialGrid,
        Quaternion cloudDrift,
        Quaternion rotQ, double zoom,
        List<PointOfInterest> pois,
        List<(string name, int cx, int cy, int poiIndex)> poiScreenOut)
    {
        surface.Clear();

        int cellW = surface.Width;
        int cellH = surface.Height;
        if (cellW <= 0 || cellH <= 0) return;

        int dotW = cellW * 2;
        int dotH = cellH * 4;
        var brailleGrid = new bool[dotW, dotH];
        var dotAlt = new double[dotW, dotH];

        double radius = Math.Min(dotW, dotH) * 0.65 * zoom;
        double centerX = dotW * 0.5;
        double centerY = dotH * 0.5;

        var rotM = Matrix4x4.CreateFromQuaternion(rotQ);

        (double px, double py, double z) Project(Vector3 v)
        {
            double vx = v.X, vy = v.Y, vz = v.Z;
            double x2 = vx * rotM.M11 + vy * rotM.M21 + vz * rotM.M31;
            double y2 = vx * rotM.M12 + vy * rotM.M22 + vz * rotM.M32;
            double z2 = vx * rotM.M13 + vy * rotM.M23 + vz * rotM.M33;
            return (centerX + x2 * radius, centerY + y2 * radius, z2);
        }

        (double px, double py, double z) ProjectWith(Vector3 v, Matrix4x4 m)
        {
            double vx = v.X, vy = v.Y, vz = v.Z;
            double x2 = vx * m.M11 + vy * m.M21 + vz * m.M31;
            double y2 = vx * m.M12 + vy * m.M22 + vz * m.M32;
            double z2 = vx * m.M13 + vy * m.M23 + vz * m.M33;
            return (centerX + x2 * radius, centerY + y2 * radius, z2);
        }

        foreach (var (a, b, level) in contourSegments)
        {
            var pa = Project(a);
            var pb = Project(b);
            if (pa.z < -0.05 && pb.z < -0.05) continue;
            DrawLineWithAlt(brailleGrid, dotAlt, dotW, dotH,
                (int)Math.Round(pa.px), (int)Math.Round(pa.py),
                (int)Math.Round(pb.px), (int)Math.Round(pb.py),
                level);
        }

        var cloudGrid = new bool[dotW, dotH];
        var cloudRotQ = Quaternion.Normalize(rotQ * cloudDrift);
        var cloudRotM = Matrix4x4.CreateFromQuaternion(cloudRotQ);
        foreach (var (csa, csb) in cloudSegments)
        {
            var pa = ProjectWith(csa, cloudRotM);
            var pb = ProjectWith(csb, cloudRotM);
            if (pa.z < -0.05 && pb.z < -0.05) continue;
            DrawLine(cloudGrid, dotW, dotH,
                (int)Math.Round(pa.px), (int)Math.Round(pa.py),
                (int)Math.Round(pb.px), (int)Math.Round(pb.py));
        }

        var cloudShadowGrid = new bool[cellW, cellH];
        var invCloudRotM = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(cloudRotQ));
        const double cloudRadius = 1.12;
        double cloudScreenRadius = radius * cloudRadius;
        for (int cy2 = 0; cy2 < cellH; cy2++)
        {
            for (int cx2 = 0; cx2 < cellW; cx2++)
            {
                double sx = (cx2 * 2 + 1 - centerX) / cloudScreenRadius;
                double sy = (cy2 * 4 + 2 - centerY) / cloudScreenRadius;
                double r2 = sx * sx + sy * sy;
                if (r2 > 1.0) continue;
                double sz = Math.Sqrt(1.0 - r2);
                double wx = sx * invCloudRotM.M11 + sy * invCloudRotM.M21 + sz * invCloudRotM.M31;
                double wy = sx * invCloudRotM.M12 + sy * invCloudRotM.M22 + sz * invCloudRotM.M32;
                double wz = sx * invCloudRotM.M13 + sy * invCloudRotM.M23 + sz * invCloudRotM.M33;
                const int spatialBuckets = 20;
                int bkx = (int)((wx + 1.5) * spatialBuckets);
                int bky = (int)((wy + 1.5) * spatialBuckets);
                int bkz = (int)((wz + 1.5) * spatialBuckets);
                int bestIdx = 0;
                double bestDist = double.MaxValue;
                for (int dxi = -1; dxi <= 1; dxi++)
                for (int dyi = -1; dyi <= 1; dyi++)
                for (int dzi = -1; dzi <= 1; dzi++)
                {
                    if (spatialGrid.TryGetValue((bkx + dxi, bky + dyi, bkz + dzi), out var bucket))
                    {
                        foreach (var idx in bucket)
                        {
                            var v = meshVertices[idx];
                            double dx = v.X - wx, dy = v.Y - wy, dz = v.Z - wz;
                            double d = dx * dx + dy * dy + dz * dz;
                            if (d < bestDist) { bestDist = d; bestIdx = idx; }
                        }
                    }
                }
                cloudShadowGrid[cx2, cy2] = cloudShadow[bestIdx];
            }
        }

        bool IsUnderCloud(int cellX, int cellY)
        {
            if (cellX < 0 || cellX >= cellW || cellY < 0 || cellY >= cellH) return false;
            return cloudShadowGrid[cellX, cellY];
        }

        for (int cy = 0; cy < cellH; cy++)
        {
            for (int cx = 0; cx < cellW; cx++)
            {
                int bx = cx * 2;
                int by = cy * 4;
                int pattern = 0;
                double bestAlt = 0;
                void CheckDot(int ox, int oy, int bit)
                {
                    int px = bx + ox, py = by + oy;
                    if (px >= 0 && px < dotW && py >= 0 && py < dotH && brailleGrid[px, py])
                    {
                        pattern |= bit;
                        bestAlt = dotAlt[px, py];
                    }
                }
                CheckDot(0, 0, 0x01); CheckDot(0, 1, 0x02); CheckDot(0, 2, 0x04);
                CheckDot(1, 0, 0x08); CheckDot(1, 1, 0x10); CheckDot(1, 2, 0x20);
                CheckDot(0, 3, 0x40); CheckDot(1, 3, 0x80);

                int cloudPattern = 0;
                if (bx < dotW && by < dotH && cloudGrid[bx, by]) cloudPattern |= 0x01;
                if (bx < dotW && by + 1 < dotH && cloudGrid[bx, by + 1]) cloudPattern |= 0x02;
                if (bx < dotW && by + 2 < dotH && cloudGrid[bx, by + 2]) cloudPattern |= 0x04;
                if (bx + 1 < dotW && by < dotH && cloudGrid[bx + 1, by]) cloudPattern |= 0x08;
                if (bx + 1 < dotW && by + 1 < dotH && cloudGrid[bx + 1, by + 1]) cloudPattern |= 0x10;
                if (bx + 1 < dotW && by + 2 < dotH && cloudGrid[bx + 1, by + 2]) cloudPattern |= 0x20;
                if (bx < dotW && by + 3 < dotH && cloudGrid[bx, by + 3]) cloudPattern |= 0x40;
                if (bx + 1 < dotW && by + 3 < dotH && cloudGrid[bx + 1, by + 3]) cloudPattern |= 0x80;

                if (cloudPattern != 0 && pattern == 0)
                {
                    surface.WriteChar(cx, cy, (char)(0x2800 + cloudPattern), foreground: Hex1bColor.FromRgb(220, 220, 230));
                }
                else if (pattern != 0)
                {
                    bool underCloud = IsUnderCloud(cx, cy);
                    if (underCloud)
                    {
                        if (cloudPattern != 0)
                        {
                            int merged = pattern | cloudPattern;
                            surface.WriteChar(cx, cy, (char)(0x2800 + merged), foreground: Hex1bColor.FromRgb(240, 240, 245));
                        }
                    }
                    else if (cloudPattern != 0)
                    {
                        int merged = pattern | cloudPattern;
                        surface.WriteChar(cx, cy, (char)(0x2800 + merged), foreground: Hex1bColor.FromRgb(240, 240, 245));
                    }
                    else
                    {
                        var color = AltitudeToColor(bestAlt);
                        surface.WriteChar(cx, cy, (char)(0x2800 + pattern), foreground: color);
                    }
                }
            }
        }

        var red = Hex1bColor.FromRgb(220, 60, 60);
        var labelColor = Hex1bColor.FromRgb(240, 240, 240);
        double sphereRadiusCells = radius / 2.0;
        double sphereCxCell = centerX / 2.0;
        double sphereCyCell = centerY / 4.0;

        poiScreenOut.Clear();
        var projected = new List<(int cx, int cy, double dist, string name, double z, int idx)>();
        for (int pi = 0; pi < pois.Count; pi++)
        {
            var poi = pois[pi];
            var (px, py, z) = Project(poi.Position * 1.02f);
            if (z < 0.05) continue;
            int cellX = (int)Math.Round(px / 2.0);
            int cellY = (int)Math.Round(py / 4.0);
            if (cellX < 0 || cellX >= cellW || cellY < 0 || cellY >= cellH) continue;
            double dxc = cellX - sphereCxCell;
            double dyc = cellY - sphereCyCell;
            projected.Add((cellX, cellY, Math.Sqrt(dxc * dxc + dyc * dyc), poi.Name, z, pi));
            poiScreenOut.Add((poi.Name, cellX, cellY, pi));
        }
        projected.Sort((a, b) => a.dist.CompareTo(b.dist));

        var occupied = new Dictionary<int, List<(int s, int e)>>();
        bool IsOccluded(int row, int s, int e)
        {
            if (!occupied.TryGetValue(row, out var ranges)) return false;
            foreach (var (rs, re) in ranges)
            {
                if (s <= re && e >= rs) return true;
            }
            return false;
        }
        void MarkOccupied(int row, int s, int e)
        {
            if (!occupied.TryGetValue(row, out var ranges))
            {
                ranges = new List<(int, int)>();
                occupied[row] = ranges;
            }
            ranges.Add((s, e));
        }

        foreach (var (cx, cy, _, name, _, _) in projected)
        {
            int labelStart = cx + 2;
            int labelEnd = labelStart + name.Length - 1;
            double labelEndDx = labelEnd - sphereCxCell;
            double labelEndDy = cy - sphereCyCell;
            double labelEndDist = Math.Sqrt(labelEndDx * labelEndDx + labelEndDy * labelEndDy);
            bool showLabel = labelEnd < cellW && labelEndDist < sphereRadiusCells * 0.85;
            int dotEnd = cx + 1;
            if (IsOccluded(cy, cx, showLabel ? labelEnd : dotEnd)) continue;
            surface.WriteChar(cx, cy, '\u28CF', foreground: red);
            surface.WriteChar(cx + 1, cy, '\u28F9', foreground: red);
            if (showLabel)
            {
                surface.WriteText(labelStart, cy, name, foreground: labelColor);
                MarkOccupied(cy, cx, labelEnd);
            }
            else
            {
                MarkOccupied(cy, cx, dotEnd);
            }
        }
    }

    private static void DrawLine(bool[,] grid, int w, int h, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h) grid[x0, y0] = true;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static void DrawLineWithAlt(bool[,] grid, double[,] altGrid,
        int w, int h, int x0, int y0, int x1, int y1, double alt)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
            {
                grid[x0, y0] = true;
                altGrid[x0, y0] = alt;
            }
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private static Hex1bColor AltitudeToColor(double alt)
    {
        if (alt < 0.3)
        {
            double t = alt / 0.3;
            return Hex1bColor.FromRgb((byte)(30 + t * 40), (byte)(60 + t * 80), (byte)(140 + t * 60));
        }
        if (alt < 0.45)
        {
            double t = (alt - 0.3) / 0.15;
            return Hex1bColor.FromRgb((byte)(70 + t * 30), (byte)(140 + t * 50), (byte)(180 - t * 40));
        }
        if (alt < 0.6)
        {
            double t = (alt - 0.45) / 0.15;
            return Hex1bColor.FromRgb((byte)(60 + t * 40), (byte)(160 + t * 40), (byte)(80 - t * 30));
        }
        if (alt < 0.75)
        {
            double t = (alt - 0.6) / 0.15;
            return Hex1bColor.FromRgb((byte)(100 + t * 80), (byte)(180 - t * 40), (byte)(50 + t * 20));
        }
        if (alt < 0.9)
        {
            double t = (alt - 0.75) / 0.15;
            return Hex1bColor.FromRgb((byte)(180 - t * 20), (byte)(140 - t * 30), (byte)(70 + t * 80));
        }
        {
            double t = Math.Min((alt - 0.9) / 0.1, 1.0);
            return Hex1bColor.FromRgb((byte)(160 + t * 80), (byte)(160 + t * 80), (byte)(170 + t * 70));
        }
    }

    private static (List<Vector3> verts, List<(int a, int b, int c)> tris, List<double> alts)
        GenerateTopoSphere(int subdivisions)
    {
        double t = (1.0 + Math.Sqrt(5.0)) / 2.0;
        var verts = new List<Vector3>
        {
            Norm(-1, t, 0), Norm(1, t, 0), Norm(-1, -t, 0), Norm(1, -t, 0),
            Norm(0, -1, t), Norm(0, 1, t), Norm(0, -1, -t), Norm(0, 1, -t),
            Norm(t, 0, -1), Norm(t, 0, 1), Norm(-t, 0, -1), Norm(-t, 0, 1),
        };
        var tris = new List<(int, int, int)>
        {
            (0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11),
            (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
            (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9),
            (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1),
        };
        var midCache = new Dictionary<long, int>();
        for (int i = 0; i < subdivisions; i++)
        {
            var newTris = new List<(int, int, int)>();
            foreach (var (a, b, c) in tris)
            {
                int ab = GetMidpoint(verts, midCache, a, b);
                int bc = GetMidpoint(verts, midCache, b, c);
                int ca = GetMidpoint(verts, midCache, c, a);
                newTris.Add((a, ab, ca));
                newTris.Add((b, bc, ab));
                newTris.Add((c, ca, bc));
                newTris.Add((ab, bc, ca));
            }
            tris = newTris;
        }
        var alts = new List<double>(verts.Count);
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            alts.Add(Fbm(v.X, v.Y, v.Z, octaves: 6));
        }
        double minA = alts.Min(), maxA = alts.Max();
        double range = maxA - minA;
        if (range < 0.001) range = 1;
        for (int i = 0; i < alts.Count; i++)
        {
            alts[i] = (alts[i] - minA) / range;
        }
        return (verts, tris, alts);
    }

    private static double Fbm(double x, double y, double z, int octaves)
    {
        double value = 0, amplitude = 1, frequency = 1.5;
        for (int o = 0; o < octaves; o++)
        {
            value += amplitude * GradientNoise(x * frequency, y * frequency, z * frequency);
            amplitude *= 0.5;
            frequency *= 2.1;
        }
        return value;
    }

    private static double GradientNoise(double x, double y, double z)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
        double fx = x - ix, fy = y - iy, fz = z - iz;
        double ux = fx * fx * (3 - 2 * fx);
        double uy = fy * fy * (3 - 2 * fy);
        double uz = fz * fz * (3 - 2 * fz);
        static double Lerp(double a, double b, double t) => a + t * (b - a);
        return Lerp(
            Lerp(
                Lerp(Hash3D(ix, iy, iz), Hash3D(ix + 1, iy, iz), ux),
                Lerp(Hash3D(ix, iy + 1, iz), Hash3D(ix + 1, iy + 1, iz), ux),
                uy),
            Lerp(
                Lerp(Hash3D(ix, iy, iz + 1), Hash3D(ix + 1, iy, iz + 1), ux),
                Lerp(Hash3D(ix, iy + 1, iz + 1), Hash3D(ix + 1, iy + 1, iz + 1), ux),
                uy),
            uz);
    }

    private static double Hash3D(int x, int y, int z)
    {
        int h = x * 374761393 + y * 668265263 + z * 1274126177;
        h = (h ^ (h >> 13)) * 1103515245;
        h = h ^ (h >> 16);
        return (h & 0x7FFFFFFF) / (double)0x7FFFFFFF;
    }

    private static int GetMidpoint(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
    {
        if (a > b) (a, b) = (b, a);
        long key = ((long)a << 32) + b;
        if (cache.TryGetValue(key, out int idx)) return idx;
        var mid = Vector3.Normalize((verts[a] + verts[b]) / 2f);
        idx = verts.Count;
        verts.Add(mid);
        cache[key] = idx;
        return idx;
    }

    private static Vector3 Norm(double x, double y, double z)
        => Vector3.Normalize(new Vector3((float)x, (float)y, (float)z));

    private static PointOfInterest Poi(string name, double lat, double lon)
    {
        double latR = lat * Math.PI / 180.0;
        double lonR = lon * Math.PI / 180.0;
        var pos = new Vector3(
            (float)(Math.Cos(latR) * Math.Sin(lonR)),
            (float)Math.Sin(latR),
            (float)(Math.Cos(latR) * Math.Cos(lonR)));
        return new PointOfInterest(name, pos);
    }

    private record PointOfInterest(string Name, Vector3 Position);
}
