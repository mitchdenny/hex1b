namespace Hex1b.Scene.Rendering;

using Hex1b.Scene.Math;

/// <summary>
/// Context for rasterizing 3D geometry to 2D screen space.
/// Handles transformation from world space → screen space and depth testing.
/// </summary>
public class SceneRasterizerContext
{
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }

    private float[] _depthBuffer;
    private Vector4[,] _fragmentBuffer;

    public SceneRasterizerContext(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
        _depthBuffer = new float[width * height];
        _fragmentBuffer = new Vector4[width, height];
        ClearBuffers();
    }

    public void ClearBuffers()
    {
        for (int i = 0; i < _depthBuffer.Length; i++)
            _depthBuffer[i] = float.MaxValue;
        
        for (int y = 0; y < ViewportHeight; y++)
            for (int x = 0; x < ViewportWidth; x++)
                _fragmentBuffer[x, y] = Vector4.Zero;
    }

    /// <summary>
    /// Transform a 3D point from world space to screen space.
    /// </summary>
    public (int screenX, int screenY, float depth) WorldToScreenSpace(Vector3 worldPos, Matrix4 viewProjMatrix)
    {
        var clipSpace = viewProjMatrix * new Vector4(worldPos, 1.0f);
        
        // Perspective divide
        float w = clipSpace.W;
        if (MathF.Abs(w) < float.Epsilon)
            return (-1, -1, 0);
        
        var ndcX = clipSpace.X / w; // [-1, 1]
        var ndcY = clipSpace.Y / w; // [-1, 1]
        var depth = clipSpace.Z / w;

        // NDC to screen coordinates
        var screenX = (int)((ndcX + 1.0f) * 0.5f * ViewportWidth);
        var screenY = (int)((1.0f - ndcY) * 0.5f * ViewportHeight); // Flip Y

        return (screenX, screenY, depth);
    }

    /// <summary>
    /// Check if a point is within the viewport bounds.
    /// </summary>
    public bool IsInViewport(int screenX, int screenY)
    {
        return screenX >= 0 && screenX < ViewportWidth && screenY >= 0 && screenY < ViewportHeight;
    }

    /// <summary>
    /// Get the depth value at a specific pixel.
    /// </summary>
    public float GetDepth(int screenX, int screenY)
    {
        if (!IsInViewport(screenX, screenY))
            return float.MaxValue;
        return _depthBuffer[screenY * ViewportWidth + screenX];
    }

    /// <summary>
    /// Set a pixel if it passes depth test.
    /// </summary>
    public bool SetPixelIfFront(int screenX, int screenY, float depth, Vector4 color)
    {
        if (!IsInViewport(screenX, screenY))
            return false;

        var idx = screenY * ViewportWidth + screenX;
        if (depth < _depthBuffer[idx])
        {
            _depthBuffer[idx] = depth;
            _fragmentBuffer[screenX, screenY] = color;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get the color at a specific pixel.
    /// </summary>
    public Vector4 GetPixel(int screenX, int screenY)
    {
        if (!IsInViewport(screenX, screenY))
            return Vector4.Zero;
        return _fragmentBuffer[screenX, screenY];
    }

    /// <summary>
    /// Bresenham line drawing algorithm for wireframe rendering.
    /// </summary>
    public void DrawLine(int x0, int y0, int x1, int y1, float depth0, float depth1, Vector4 color)
    {
        var dx = MathF.Abs(x1 - x0);
        var dy = MathF.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        int x = x0, y = y0;
        int steps = (int)MathF.Max(dx, dy) + 1;

        for (int i = 0; i <= steps; i++)
        {
            var t = steps > 0 ? (float)i / steps : 0;
            var depth = MathHelper.Lerp(depth0, depth1, t);
            SetPixelIfFront(x, y, depth, color);

            if (x == x1 && y == y1)
                break;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

        /// <summary>
        /// Rasterize a filled triangle using barycentric coordinates and depth interpolation.
        /// </summary>
        public void DrawFilledTriangle(
            int x0, int y0, float depth0,
            int x1, int y1, float depth1,
            int x2, int y2, float depth2,
            Vector4 color)
        {
            var minX = global::System.Math.Max(0, global::System.Math.Min(x0, global::System.Math.Min(x1, x2)));
            var maxX = global::System.Math.Min(ViewportWidth - 1, global::System.Math.Max(x0, global::System.Math.Max(x1, x2)));
            var minY = global::System.Math.Max(0, global::System.Math.Min(y0, global::System.Math.Min(y1, y2)));
            var maxY = global::System.Math.Min(ViewportHeight - 1, global::System.Math.Max(y0, global::System.Math.Max(y1, y2)));

            var area = EdgeFunction(x0, y0, x1, y1, x2, y2);
            if (MathF.Abs(area) < float.Epsilon)
                return;

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var w0 = EdgeFunction(x1, y1, x2, y2, x, y);
                    var w1 = EdgeFunction(x2, y2, x0, y0, x, y);
                    var w2 = EdgeFunction(x0, y0, x1, y1, x, y);

                    var inside = area > 0
                        ? (w0 >= 0 && w1 >= 0 && w2 >= 0)
                        : (w0 <= 0 && w1 <= 0 && w2 <= 0);

                    if (!inside)
                        continue;

                    w0 /= area;
                    w1 /= area;
                    w2 /= area;

                    var depth = (w0 * depth0) + (w1 * depth1) + (w2 * depth2);
                    SetPixelIfFront(x, y, depth, color);
                }
            }
        }

        /// <summary>
        /// Rasterize a filled triangle with UV coordinate interpolation for texture sampling.
        /// </summary>
        public void DrawFilledTriangleWithUV(
            int x0, int y0, float depth0, float u0, float v0,
            int x1, int y1, float depth1, float u1, float v1,
            int x2, int y2, float depth2, float u2, float v2,
            Func<float, float, Vector4> sampleColor)
        {
            var minX = global::System.Math.Max(0, global::System.Math.Min(x0, global::System.Math.Min(x1, x2)));
            var maxX = global::System.Math.Min(ViewportWidth - 1, global::System.Math.Max(x0, global::System.Math.Max(x1, x2)));
            var minY = global::System.Math.Max(0, global::System.Math.Min(y0, global::System.Math.Min(y1, y2)));
            var maxY = global::System.Math.Min(ViewportHeight - 1, global::System.Math.Max(y0, global::System.Math.Max(y1, y2)));

            var area = EdgeFunction(x0, y0, x1, y1, x2, y2);
            if (MathF.Abs(area) < float.Epsilon)
                return;

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var w0 = EdgeFunction(x1, y1, x2, y2, x, y);
                    var w1 = EdgeFunction(x2, y2, x0, y0, x, y);
                    var w2 = EdgeFunction(x0, y0, x1, y1, x, y);

                    var inside = area > 0
                        ? (w0 >= 0 && w1 >= 0 && w2 >= 0)
                        : (w0 <= 0 && w1 <= 0 && w2 <= 0);

                    if (!inside)
                        continue;

                    w0 /= area;
                    w1 /= area;
                    w2 /= area;

                    var depth = (w0 * depth0) + (w1 * depth1) + (w2 * depth2);
                    var u = (w0 * u0) + (w1 * u1) + (w2 * u2);
                    var v = (w0 * v0) + (w1 * v1) + (w2 * v2);

                    var color = sampleColor(u, v);
                    SetPixelIfFront(x, y, depth, color);
                }
            }
        }

        private static float EdgeFunction(int ax, int ay, int bx, int by, int px, int py)
        {
            return ((px - ax) * (by - ay)) - ((py - ay) * (bx - ax));
        }
    }

    /// <summary>
    /// Helper math functions for rendering.
    /// </summary>
internal static class MathHelper
{
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    public static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
