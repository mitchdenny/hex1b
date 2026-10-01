namespace Hex1b.Scene.Materials;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// Material for rendering lines and wireframes.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneLineBasicMaterial : BaseSceneMaterial
{
    public float LineWidth { get; set; } = 1.0f;

    public SceneLineBasicMaterial() : base()
    {
    }

    public SceneLineBasicMaterial(Vector3 color) : base(color)
    {
    }
}
