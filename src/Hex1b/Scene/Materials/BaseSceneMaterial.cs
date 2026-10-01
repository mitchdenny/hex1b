namespace Hex1b.Scene.Materials;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Scene.Math;

/// <summary>
/// Base class for all materials that define how objects are rendered.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public abstract class BaseSceneMaterial
{
    public Vector3 Color { get; set; } = Vector3.One;
    public bool Wireframe { get; set; } = false;

    public BaseSceneMaterial()
    {
    }

    public BaseSceneMaterial(Vector3 color)
    {
        Color = color;
    }
}
