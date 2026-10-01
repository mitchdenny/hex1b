namespace Hex1b.Scene.Materials;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// Material for rendering filled surfaces.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneMeshMaterial : BaseSceneMaterial
{
    public bool Lit { get; set; } = true; // Use lighting calculations
    public SceneMeshShadingMode ShadingMode { get; set; } = SceneMeshShadingMode.Lit;

    public SceneMeshMaterial() : base()
    {
    }

    public SceneMeshMaterial(Vector3 color) : base(color)
    {
    }
}
