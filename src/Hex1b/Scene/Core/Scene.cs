namespace Hex1b.Scene.Core;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The root of the 3D scene graph hierarchy.
/// Contains all objects, cameras, and lights for a 3D scene.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class Scene : SceneObjectGroup
{
    public Scene() : base("Scene")
    {
    }

    public Scene(string? name) : base(name)
    {
    }
}
