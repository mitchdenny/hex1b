namespace Hex1b.Scene.Objects;

using Hex1b.Scene.Core;
using Hex1b.Scene.Geometry;
using Hex1b.Scene.Materials;

/// <summary>
/// Represents a mesh: a combination of geometry and material.
/// </summary>
public class SceneMesh : SceneObject
{
    public SceneBufferGeometry Geometry { get; set; }
    public BaseSceneMaterial Material { get; set; }

    public SceneMesh(SceneBufferGeometry geometry, BaseSceneMaterial material) : base()
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Material = material ?? throw new ArgumentNullException(nameof(material));
    }

    public SceneMesh(SceneBufferGeometry geometry, BaseSceneMaterial material, string? name)
        : base(name)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Material = material ?? throw new ArgumentNullException(nameof(material));
    }
}
