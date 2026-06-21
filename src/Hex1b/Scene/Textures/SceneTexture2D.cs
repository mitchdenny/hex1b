namespace Hex1b.Scene.Textures;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents a 2D texture with RGBA32 pixel data.
/// Supports dynamic updates for per-frame rendering (e.g., widget-to-texture rendering).
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneTexture2D
{
    private uint[] _pixels;
    private readonly int _width;
    private readonly int _height;

    public int Width => _width;
    public int Height => _height;

    /// <summary>
    /// Create a new 2D texture with specified dimensions.
    /// Pixels are initialized to opaque white (RGBA 255, 255, 255, 255).
    /// </summary>
    public SceneTexture2D(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Texture dimensions must be positive");

        _width = width;
        _height = height;
        _pixels = new uint[width * height];
        
        // Initialize to opaque white
        for (int i = 0; i < _pixels.Length; i++)
            _pixels[i] = 0xFFFFFFFFu;
    }

    /// <summary>
    /// Get pixel at (x, y) as RGBA32 uint.
    /// </summary>
    public uint GetPixel(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return 0;
        return _pixels[y * _width + x];
    }

    /// <summary>
    /// Set pixel at (x, y) to RGBA32 value.
    /// </summary>
    public void SetPixel(int x, int y, uint rgba)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            return;
        _pixels[y * _width + x] = rgba;
    }

    /// <summary>
    /// Set pixel at (x, y) from RGBA components (0-255 each).
    /// </summary>
    public void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
    {
        uint rgba = ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
        SetPixel(x, y, rgba);
    }

    /// <summary>
    /// Replace entire texture buffer with new pixel data.
    /// Must be exactly width × height pixels.
    /// </summary>
    public void SetPixels(uint[] pixels)
    {
        if (pixels.Length != _width * _height)
            throw new ArgumentException(
                $"Pixel array length ({pixels.Length}) must match texture size ({_width}×{_height})",
                nameof(pixels)
            );
        Array.Copy(pixels, _pixels, pixels.Length);
    }

    /// <summary>
    /// Get a copy of the entire pixel buffer.
    /// </summary>
    public uint[] GetPixels()
    {
        return (uint[])_pixels.Clone();
    }

    /// <summary>
    /// Sample texture at (u, v) coordinates [0, 1] × [0, 1] using bilinear filtering.
    /// Returns RGBA32 value.
    /// </summary>
    public uint SampleBilinear(float u, float v, TextureWrapMode wrapMode = TextureWrapMode.Clamp)
    {
        // Wrap coordinates based on wrap mode
        (u, v) = WrapCoordinates(u, v, wrapMode);

        // Convert normalized coordinates to pixel coordinates
        float pixelX = u * (_width - 1);
        float pixelY = v * (_height - 1);

        int x0 = (int)pixelX;
        int y0 = (int)pixelY;
        int x1 = System.Math.Min(x0 + 1, _width - 1);
        int y1 = System.Math.Min(y0 + 1, _height - 1);

        float fx = pixelX - x0;
        float fy = pixelY - y0;

        // Sample four corners
        uint p00 = GetPixel(x0, y0);
        uint p10 = GetPixel(x1, y0);
        uint p01 = GetPixel(x0, y1);
        uint p11 = GetPixel(x1, y1);

        // Bilinear interpolation
        return BilinearBlend(p00, p10, p01, p11, fx, fy);
    }

    private (float, float) WrapCoordinates(float u, float v, TextureWrapMode mode)
    {
        return mode switch
        {
            TextureWrapMode.Clamp => (System.Math.Clamp(u, 0f, 1f), System.Math.Clamp(v, 0f, 1f)),
            TextureWrapMode.Repeat => (u - (float)System.Math.Floor(u), v - (float)System.Math.Floor(v)),
            TextureWrapMode.MirrorRepeat =>
            (
                MirrorWrap(u),
                MirrorWrap(v)
            ),
            _ => (System.Math.Clamp(u, 0f, 1f), System.Math.Clamp(v, 0f, 1f))
        };
    }

    private static float MirrorWrap(float t)
    {
        // Map t to [0, 2) range, then mirror
        t = t - (float)System.Math.Floor(t / 2f) * 2f;
        if (t > 1f)
            t = 2f - t;
        return t;
    }

    private static uint BilinearBlend(uint p00, uint p10, uint p01, uint p11, float fx, float fy)
    {
        // Unpack RGBA
        var (r00, g00, b00, a00) = Unpack(p00);
        var (r10, g10, b10, a10) = Unpack(p10);
        var (r01, g01, b01, a01) = Unpack(p01);
        var (r11, g11, b11, a11) = Unpack(p11);

        // Bilinear blend each channel
        float r = Lerp(Lerp(r00, r10, fx), Lerp(r01, r11, fx), fy);
        float g = Lerp(Lerp(g00, g10, fx), Lerp(g01, g11, fx), fy);
        float b = Lerp(Lerp(b00, b10, fx), Lerp(b01, b11, fx), fy);
        float a = Lerp(Lerp(a00, a10, fx), Lerp(a01, a11, fx), fy);

        return Pack((byte)r, (byte)g, (byte)b, (byte)a);
    }

    private static (float, float, float, float) Unpack(uint rgba)
    {
        byte r = (byte)(rgba >> 24);
        byte g = (byte)(rgba >> 16);
        byte b = (byte)(rgba >> 8);
        byte a = (byte)rgba;
        return (r, g, b, a);
    }

    private static uint Pack(byte r, byte g, byte b, byte a)
    {
        return ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}

[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public enum TextureWrapMode
{
    Clamp,
    Repeat,
    MirrorRepeat
}

[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public enum TextureFilterMode
{
    Nearest,
    Linear
}
