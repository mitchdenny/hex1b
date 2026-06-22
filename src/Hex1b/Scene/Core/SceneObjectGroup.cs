namespace Hex1b.Scene.Core;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A container for scene objects, allowing grouping and hierarchical organization.
/// </summary>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public class SceneObjectGroup : SceneObject
{
    public SceneObjectGroup() : base()
    {
    }

    public SceneObjectGroup(string? name) : base(name)
    {
    }
}
