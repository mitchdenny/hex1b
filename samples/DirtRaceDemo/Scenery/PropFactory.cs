namespace DirtRaceDemo.Scenery;

using DirtRaceDemo.Physics;
using Hex1b.Scene.Materials;
using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// Builds simple scenery props: blocking obstacle boxes and a start/finish marker.
/// </summary>
public static class PropFactory
{
    private static readonly Vector3 ObstacleColor = new(0.40f, 0.42f, 0.45f);
    private static readonly Vector3 MarkerColor = new(0.95f, 0.95f, 0.98f);

    public static SceneMesh BuildObstacle(Obstacle obstacle)
    {
        var material = new SceneMeshMaterial(ObstacleColor) { ShadingMode = SceneMeshShadingMode.Lit };
        var geometry = GeometryFactory.Box(obstacle.HalfX * 2.0f, obstacle.Height, obstacle.HalfZ * 2.0f);

        return new SceneMesh(geometry, material, "Obstacle")
        {
            Position = new Vector3(obstacle.Center.X, obstacle.Height * 0.5f, obstacle.Center.Y),
        };
    }

    /// <summary>A flat painted strip lying on the track marking the start/finish line.</summary>
    public static SceneMesh BuildStartMarker(Vector2 position, float headingAngle, float width)
    {
        var material = new SceneMeshMaterial(MarkerColor) { ShadingMode = SceneMeshShadingMode.Lit };
        // Long across the track (X), thin along travel (Z), and almost flat so it never
        // occludes the vehicle that spawns on top of it.
        var geometry = GeometryFactory.Box(width, 0.08f, 1.0f);

        return new SceneMesh(geometry, material, "StartLine")
        {
            Position = new Vector3(position.X, 0.05f, position.Y),
            Rotation = Quaternion.FromEulerAngles(0.0f, headingAngle, 0.0f),
        };
    }
}
