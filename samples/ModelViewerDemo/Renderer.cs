using System.Numerics;

namespace ModelViewerDemo;

/// <summary>
/// Renders a 3D mesh to a BrailleBuffer using orthographic projection
/// with z-buffer based hidden-line removal.
/// </summary>
internal static class Renderer
{
    /// <summary>
    /// Render the mesh into the braille buffer with the given rotation.
    /// </summary>
    public static void Render(Mesh mesh, BrailleBuffer buffer, Quaternion rotation)
    {
        buffer.Clear();

        var rotMatrix = Matrix4x4.CreateFromQuaternion(rotation);
        var viewDir = new Vector3(0, 0, -1); // looking into the screen

        int dotW = buffer.DotWidth;
        int dotH = buffer.DotHeight;

        // Transform all vertices
        Span<Vector3> transformed = mesh.Vertices.Length <= 4096
            ? stackalloc Vector3[mesh.Vertices.Length]
            : new Vector3[mesh.Vertices.Length];

        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            transformed[i] = Vector3.Transform(mesh.Vertices[i], rotMatrix);
        }

        // Determine front-facing faces
        Span<bool> faceFrontFacing = mesh.Faces.Length <= 4096
            ? stackalloc bool[mesh.Faces.Length]
            : new bool[mesh.Faces.Length];

        for (int i = 0; i < mesh.Faces.Length; i++)
        {
            var a = transformed[mesh.Faces[i].A];
            var b = transformed[mesh.Faces[i].B];
            var c = transformed[mesh.Faces[i].C];
            var normal = Vector3.Cross(b - a, c - a);
            faceFrontFacing[i] = Vector3.Dot(normal, viewDir) < 0;
        }

        // Compute projection scale: fit [-1,1] into dot buffer with margin
        float margin = 2f;
        float scaleX = (dotW - margin * 2) / 2f;
        float scaleY = (dotH - margin * 2) / 2f;
        float scale = MathF.Min(scaleX, scaleY);
        float centerX = dotW / 2f;
        float centerY = dotH / 2f;

        // Build z-buffer by rasterizing all front-facing triangles
        var depthBuffer = new float[dotW * dotH];
        Array.Fill(depthBuffer, float.MaxValue);

        for (int fi = 0; fi < mesh.Faces.Length; fi++)
        {
            if (!faceFrontFacing[fi]) continue;

            var face = mesh.Faces[fi];
            var va = transformed[face.A];
            var vb = transformed[face.B];
            var vc = transformed[face.C];

            // Project to dot space
            float ax = centerX + va.X * scale, ay = centerY - va.Y * scale, az = va.Z;
            float bx = centerX + vb.X * scale, by = centerY - vb.Y * scale, bz = vb.Z;
            float cx = centerX + vc.X * scale, cy = centerY - vc.Y * scale, cz = vc.Z;

            RasterizeTriangle(depthBuffer, dotW, dotH, ax, ay, az, bx, by, bz, cx, cy, cz);
        }

        // Draw edges where at least one adjacent face is front-facing,
        // but only pixels that pass the depth test
        const float depthBias = 0.02f;

        for (int ei = 0; ei < mesh.Edges.Length; ei++)
        {
            var adjFaces = mesh.EdgeAdjacentFaces[ei];
            bool visible = false;
            foreach (var fi in adjFaces)
            {
                if (faceFrontFacing[fi])
                {
                    visible = true;
                    break;
                }
            }

            if (!visible) continue;

            var edge = mesh.Edges[ei];
            var va = transformed[edge.A];
            var vb = transformed[edge.B];

            // Project to dot space
            float x0f = centerX + va.X * scale;
            float y0f = centerY - va.Y * scale;
            float z0 = va.Z;
            float x1f = centerX + vb.X * scale;
            float y1f = centerY - vb.Y * scale;
            float z1 = vb.Z;

            DrawLineDepthTested(buffer, depthBuffer, dotW, dotH,
                x0f, y0f, z0, x1f, y1f, z1, depthBias);
        }
    }

    /// <summary>
    /// Rasterize a triangle into the depth buffer using scanline algorithm.
    /// </summary>
    private static void RasterizeTriangle(
        float[] depthBuffer, int width, int height,
        float ax, float ay, float az,
        float bx, float by, float bz,
        float cx, float cy, float cz)
    {
        // Bounding box
        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, cx))));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, cx))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, cy))));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, cy))));

        // Precompute edge function denominators
        float denom = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
        if (MathF.Abs(denom) < 1e-6f) return; // degenerate triangle
        float invDenom = 1f / denom;

        for (int py = minY; py <= maxY; py++)
        {
            for (int px = minX; px <= maxX; px++)
            {
                float ppx = px + 0.5f;
                float ppy = py + 0.5f;

                // Barycentric coordinates
                float w0 = ((by - cy) * (ppx - cx) + (cx - bx) * (ppy - cy)) * invDenom;
                float w1 = ((cy - ay) * (ppx - cx) + (ax - cx) * (ppy - cy)) * invDenom;
                float w2 = 1f - w0 - w1;

                if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                float z = w0 * az + w1 * bz + w2 * cz;
                int idx = py * width + px;
                if (z < depthBuffer[idx])
                {
                    depthBuffer[idx] = z;
                }
            }
        }
    }

    /// <summary>
    /// Draw a line with per-pixel depth testing against the z-buffer.
    /// </summary>
    private static void DrawLineDepthTested(
        BrailleBuffer buffer, float[] depthBuffer, int width, int height,
        float x0, float y0, float z0, float x1, float y1, float z1, float bias)
    {
        int ix0 = (int)x0, iy0 = (int)y0;
        int ix1 = (int)x1, iy1 = (int)y1;

        int dx = Math.Abs(ix1 - ix0);
        int dy = Math.Abs(iy1 - iy0);
        int sx = ix0 < ix1 ? 1 : -1;
        int sy = iy0 < iy1 ? 1 : -1;
        int err = dx - dy;

        int steps = Math.Max(dx, dy);
        if (steps == 0) steps = 1;

        int step = 0;
        int cx = ix0, cy = iy0;

        while (true)
        {
            // Interpolate Z
            float t = steps > 0 ? (float)step / steps : 0;
            float z = z0 + t * (z1 - z0);

            if (cx >= 0 && cx < width && cy >= 0 && cy < height)
            {
                int idx = cy * width + cx;
                if (z <= depthBuffer[idx] + bias)
                {
                    buffer.SetDot(cx, cy);
                }
            }

            if (cx == ix1 && cy == iy1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; cx += sx; }
            if (e2 < dx) { err += dx; cy += sy; }
            step++;
        }
    }
}
