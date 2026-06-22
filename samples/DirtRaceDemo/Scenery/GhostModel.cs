namespace DirtRaceDemo.Scenery;

using Hex1b.Scene.Core;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// A translucent-looking "ghost" of the truck used to replay the player's best lap. It mirrors the
/// silhouette of <see cref="VehicleModel"/> (chassis + cabin + wheels) but is drawn as an unlit
/// wireframe in a cool tint so it reads as a phantom rather than a second solid vehicle.
/// </summary>
public sealed class GhostModel
{
    private const float WheelRadius = 0.4f;

    public SceneObjectGroup Root { get; }

    public GhostModel()
    {
        Root = new SceneObjectGroup("Ghost");

        var ghostMaterial = new SceneMeshMaterial(new Vector3(0.55f, 0.85f, 1.0f))
        {
            Lit = false,
            ShadingMode = SceneMeshShadingMode.Lit,
            Wireframe = true,
        };

        var chassis = new SceneMesh(GeometryFactory.Box(1.5f, 0.5f, 2.6f), ghostMaterial, "GhostChassis")
        {
            Position = new Vector3(0.0f, 0.38f, 0.0f),
        };
        var cabin = new SceneMesh(GeometryFactory.Box(1.2f, 0.55f, 1.1f), ghostMaterial, "GhostCabin")
        {
            Position = new Vector3(0.0f, 0.82f, -0.25f),
        };

        Root.AddChild(chassis);
        Root.AddChild(cabin);

        Root.AddChild(CreateWheel(ghostMaterial, -0.8f, 0.85f));
        Root.AddChild(CreateWheel(ghostMaterial, 0.8f, 0.85f));
        Root.AddChild(CreateWheel(ghostMaterial, -0.8f, -0.85f));
        Root.AddChild(CreateWheel(ghostMaterial, 0.8f, -0.85f));
    }

    private static SceneObject CreateWheel(BaseSceneMaterial material, float x, float z)
    {
        return new SceneMesh(GeometryFactory.WheelCylinder(WheelRadius, 0.32f), material, "GhostWheel")
        {
            Position = new Vector3(x, WheelRadius, z),
        };
    }

    /// <summary>Places the ghost at a recorded world position facing the recorded heading.</summary>
    public void SetTransform(Vector3 worldPosition, float heading)
    {
        Root.Position = worldPosition;
        Root.Rotation = Quaternion.FromEulerAngles(0.0f, heading, 0.0f);
    }
}
