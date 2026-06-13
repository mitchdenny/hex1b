namespace Hex1b.Scene.Core;

/// <summary>
/// The root of the 3D scene graph hierarchy.
/// Contains all objects, cameras, and lights for a 3D scene.
/// </summary>
public class Scene : SceneObjectGroup
{
    public Scene() : base("Scene")
    {
    }

    public Scene(string? name) : base(name)
    {
    }
}
