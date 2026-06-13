namespace Hex1b.Scene.Textures;

using Hex1b.Scene.Core;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;
using Hex1b.Scene.Rendering;

/// <summary>
/// Renders a <see cref="Scene"/> from a <see cref="SceneCamera"/> directly into a
/// <see cref="SceneTexture2D"/> (render-to-texture).
/// <para>
/// This is the simpler sibling of <see cref="TerminalTexture"/>: instead of routing a
/// scene through a terminal's cell buffer and reconstructing sub-cell coverage, it
/// rasterizes the scene straight to RGBA pixels, giving a clean, full-resolution texture
/// that can be applied to mesh geometry.
/// </para>
/// </summary>
public sealed class SceneRenderTexture
{
    private readonly SceneRenderer _renderer;
    private SceneRasterizerContext _context;
    private SceneTexture2D _texture;
    private int _width;
    private int _height;

    /// <summary>
    /// The scene rendered into the texture.
    /// </summary>
    public Scene Scene { get; set; }

    /// <summary>
    /// The camera the scene is rendered from.
    /// </summary>
    public SceneCamera Camera { get; set; }

    /// <summary>
    /// The texture written by the most recent <see cref="Update"/>.
    /// </summary>
    public SceneTexture2D Texture => _texture;

    /// <summary>
    /// Pixel width of the render target.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Pixel height of the render target.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Create a render-to-texture for the given scene and camera.
    /// </summary>
    /// <param name="scene">Scene to render.</param>
    /// <param name="camera">Camera to render from.</param>
    /// <param name="width">Render target width in pixels.</param>
    /// <param name="height">Render target height in pixels.</param>
    /// <param name="renderer">Optional renderer to reuse; a default one is created when null.</param>
    public SceneRenderTexture(Scene scene, SceneCamera camera, int width = 128, int height = 128, SceneRenderer? renderer = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Render texture dimensions must be positive.");
        }

        Scene = scene;
        Camera = camera;
        _width = width;
        _height = height;
        _renderer = renderer ?? new SceneRenderer();
        _context = new SceneRasterizerContext(width, height);
        _texture = new SceneTexture2D(width, height);
    }

    /// <summary>
    /// Resize the render target, reallocating the rasterizer context and texture.
    /// No-op when the dimensions are unchanged.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Render texture dimensions must be positive.");
        }

        if (width == _width && height == _height)
        {
            return;
        }

        _width = width;
        _height = height;
        _context = new SceneRasterizerContext(width, height);
        _texture = new SceneTexture2D(width, height);
    }

    /// <summary>
    /// Render the current scene from the current camera into the texture and return it.
    /// </summary>
    public SceneTexture2D Update()
    {
        _renderer.Render(Scene, Camera, _context);

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var color = _context.GetPixel(x, y);
                _texture.SetPixel(
                    x,
                    y,
                    ToByte(color.X),
                    ToByte(color.Y),
                    ToByte(color.Z),
                    ToByte(color.W));
            }
        }

        return _texture;
    }

    private static byte ToByte(float value)
    {
        var scaled = value * 255.0f;
        if (scaled <= 0.0f)
        {
            return 0;
        }

        if (scaled >= 255.0f)
        {
            return 255;
        }

        return (byte)(scaled + 0.5f);
    }
}
