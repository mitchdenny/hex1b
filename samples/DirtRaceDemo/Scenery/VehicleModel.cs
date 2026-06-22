namespace DirtRaceDemo.Scenery;

using DirtRaceDemo.Physics;
using Hex1b.Scene.Core;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// The visual 4x4 truck: a <see cref="SceneObjectGroup"/> of boxes (chassis + cabin) and four
/// wheels, built nose-forward along +Z so a Y rotation by the vehicle heading points it the
/// right way. <see cref="SyncTo"/> copies the simulation transform onto the group each frame.
/// </summary>
public sealed class VehicleModel
{
    private const float WheelRadius = 0.4f;

    private readonly SceneObject _frontLeft;
    private readonly SceneObject _frontRight;
    private readonly SceneObject _rearLeft;
    private readonly SceneObject _rearRight;

    public SceneObjectGroup Root { get; }

    public VehicleModel()
    {
        Root = new SceneObjectGroup("Truck");

        var bodyMaterial = new SceneMeshMaterial(new Vector3(0.82f, 0.28f, 0.14f)) { ShadingMode = SceneMeshShadingMode.Lit };
        var cabinMaterial = new SceneMeshMaterial(new Vector3(0.92f, 0.74f, 0.22f)) { ShadingMode = SceneMeshShadingMode.Lit };
        var wheelMaterial = new SceneMeshMaterial(new Vector3(0.10f, 0.10f, 0.12f)) { ShadingMode = SceneMeshShadingMode.Lit };

        var chassis = new SceneMesh(GeometryFactory.Box(1.5f, 0.5f, 2.6f), bodyMaterial, "Chassis")
        {
            Position = new Vector3(0.0f, 0.38f, 0.0f),
        };
        var cabin = new SceneMesh(GeometryFactory.Box(1.2f, 0.55f, 1.1f), cabinMaterial, "Cabin")
        {
            Position = new Vector3(0.0f, 0.82f, -0.25f),
        };

        Root.AddChild(chassis);
        Root.AddChild(cabin);

        _frontLeft = CreateWheel(wheelMaterial, -0.8f, 0.85f);
        _frontRight = CreateWheel(wheelMaterial, 0.8f, 0.85f);
        _rearLeft = CreateWheel(wheelMaterial, -0.8f, -0.85f);
        _rearRight = CreateWheel(wheelMaterial, 0.8f, -0.85f);

        Root.AddChild(_frontLeft);
        Root.AddChild(_frontRight);
        Root.AddChild(_rearLeft);
        Root.AddChild(_rearRight);
    }

    private static SceneObject CreateWheel(BaseSceneMaterial material, float x, float z)
    {
        return new SceneMesh(GeometryFactory.WheelCylinder(WheelRadius, 0.32f), material, "Wheel")
        {
            Position = new Vector3(x, WheelRadius, z),
        };
    }

    public void SyncTo(ArcadeVehicle vehicle)
    {
        Root.Position = vehicle.WorldPosition;

        // Slight forward pitch while airborne reads as the truck nosing over a jump.
        var pitch = vehicle.Grounded ? 0.0f : MathF.Max(-0.35f, -vehicle.VerticalVelocity * 0.03f);
        Root.Rotation = Quaternion.FromEulerAngles(pitch, vehicle.Heading, 0.0f);
    }

    /// <summary>Visually steer the front wheels by the given angle (radians).</summary>
    public void SetSteer(float angle)
    {
        var steer = Quaternion.FromEulerAngles(0.0f, angle, 0.0f);
        _frontLeft.Rotation = steer;
        _frontRight.Rotation = steer;
    }
}
