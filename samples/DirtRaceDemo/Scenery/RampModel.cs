namespace DirtRaceDemo.Scenery;

using DirtRaceDemo.Physics;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// Builds the wedge mesh for a <see cref="Ramp"/>, positioned and oriented so its slope and lip
/// line up with the ramp's physical footprint.
/// </summary>
public static class RampModel
{
    private static readonly Vector3 RampColor = new(0.55f, 0.40f, 0.24f);

    public static SceneMesh Build(Ramp ramp)
    {
        var material = new SceneMeshMaterial(RampColor) { ShadingMode = SceneMeshShadingMode.Lit };
        var geometry = GeometryFactory.RampWedge(ramp.Width, ramp.Length, ramp.Height);
        var center = ramp.MeshCenter;

        return new SceneMesh(geometry, material, "Ramp")
        {
            Position = new Vector3(center.X, 0.0f, center.Y),
            Rotation = Quaternion.FromEulerAngles(0.0f, ramp.HeadingAngle, 0.0f),
        };
    }
}
