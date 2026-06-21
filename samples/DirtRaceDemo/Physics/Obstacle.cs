namespace DirtRaceDemo.Physics;

using Hex1b.Scene.Math;

/// <summary>
/// A solid, axis-aligned obstacle (rock, crate) that blocks the vehicle while it is below the
/// obstacle's top. Defined by an XZ centre, half extents and a height.
/// </summary>
public sealed class Obstacle
{
    public Vector2 Center { get; }
    public float HalfX { get; }
    public float HalfZ { get; }
    public float Height { get; }

    public Obstacle(Vector2 center, float halfX, float halfZ, float height)
    {
        Center = center;
        HalfX = halfX;
        HalfZ = halfZ;
        Height = height;
    }

    public bool Contains(Vector2 point)
    {
        return MathF.Abs(point.X - Center.X) <= HalfX
            && MathF.Abs(point.Y - Center.Y) <= HalfZ;
    }
}
