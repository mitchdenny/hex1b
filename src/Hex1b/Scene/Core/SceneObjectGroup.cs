namespace Hex1b.Scene.Core;

/// <summary>
/// A container for scene objects, allowing grouping and hierarchical organization.
/// </summary>
public class SceneObjectGroup : SceneObject
{
    public SceneObjectGroup() : base()
    {
    }

    public SceneObjectGroup(string? name) : base(name)
    {
    }
}
