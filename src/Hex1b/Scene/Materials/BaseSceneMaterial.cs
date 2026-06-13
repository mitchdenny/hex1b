namespace Hex1b.Scene.Materials;

using Hex1b.Scene.Math;

/// <summary>
/// Base class for all materials that define how objects are rendered.
/// </summary>
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

/// <summary>
/// Material for rendering lines and wireframes.
/// </summary>
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

/// <summary>
/// Material for rendering filled surfaces.
/// </summary>
public class SceneMeshMaterial : BaseSceneMaterial
{
    public bool Lit { get; set; } = true; // Use lighting calculations

    public SceneMeshMaterial() : base()
    {
    }

    public SceneMeshMaterial(Vector3 color) : base(color)
    {
    }
}
