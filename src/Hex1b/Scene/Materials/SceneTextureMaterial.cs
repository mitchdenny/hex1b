namespace Hex1b.Scene.Materials;

using Hex1b.Scene.Math;
using Hex1b.Scene.Textures;
using Hex1b.Widgets;

/// <summary>
/// Material that renders a texture on mesh surfaces.
/// Can optionally render a widget as the texture source, updating per-frame for dynamic content.
/// </summary>
public class SceneTextureMaterial : SceneMeshMaterial
{
    private SceneTexture2D? _texture;
    private Hex1bWidget? _widgetSource;

    /// <summary>
    /// The 2D texture to sample during rendering.
    /// </summary>
    public SceneTexture2D? Texture
    {
        get => _texture;
        set => _texture = value;
    }

    /// <summary>
    /// Optional widget to render as the texture source.
    /// When set, the widget is rendered each frame to update the texture.
    /// </summary>
    public Hex1bWidget? WidgetSource
    {
        get => _widgetSource;
        set => _widgetSource = value;
    }

    /// <summary>
    /// Number of terminal columns the <see cref="WidgetSource"/> widget is rendered into
    /// when sampled as a texture. Larger values capture more detail at higher cost.
    /// </summary>
    public int WidgetSourceColumns { get; set; } = 48;

    /// <summary>
    /// Number of terminal rows the <see cref="WidgetSource"/> widget is rendered into
    /// when sampled as a texture.
    /// </summary>
    public int WidgetSourceRows { get; set; } = 24;

    /// <summary>
    /// Texture wrap mode (Clamp, Repeat, MirrorRepeat).
    /// </summary>
    public TextureWrapMode WrapMode { get; set; } = TextureWrapMode.Clamp;

    /// <summary>
    /// Texture filter mode (Nearest, Linear).
    /// </summary>
    public TextureFilterMode FilterMode { get; set; } = TextureFilterMode.Linear;

    public SceneTextureMaterial() : base()
    {
    }

    public SceneTextureMaterial(Vector3 color) : base(color)
    {
    }

    public SceneTextureMaterial(SceneTexture2D texture) : base()
    {
        _texture = texture;
    }

    public SceneTextureMaterial(Vector3 color, SceneTexture2D texture) : base(color)
    {
        _texture = texture;
    }
}
