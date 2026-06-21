namespace DirtRaceDemo.Physics;

using Hex1b.Scene.Math;

/// <summary>
/// A launch ramp. Vehicles climb the linear slope from <see cref="FrontCenter"/> along
/// <see cref="Heading"/> and fly off the lip at the far end, where the surface drops back to
/// ground level so accumulated vertical momentum becomes airtime.
/// </summary>
public sealed class Ramp
{
    public Vector2 FrontCenter { get; }
    public Vector2 Heading { get; }
    public float Length { get; }
    public float Width { get; }
    public float Height { get; }

    private readonly Vector2 _right;

    public Ramp(Vector2 frontCenter, Vector2 heading, float length, float width, float height)
    {
        FrontCenter = frontCenter;
        Heading = heading.Normalized;
        Length = length;
        Width = width;
        Height = height;
        _right = new Vector2(Heading.Y, -Heading.X);
    }

    /// <summary>The world XZ position of the ramp geometry centre.</summary>
    public Vector2 MeshCenter => FrontCenter + Heading * (Length * 0.5f);

    /// <summary>The Y rotation (radians) that aligns the wedge's +Z lip with the heading.</summary>
    public float HeadingAngle => MathF.Atan2(Heading.X, Heading.Y);

    /// <summary>Surface height contributed by this ramp at an XZ point (0 outside its footprint).</summary>
    public float HeightAt(float x, float z)
    {
        var rel = new Vector2(x, z) - FrontCenter;
        var s = Vector2.Dot(rel, Heading);
        var lateral = Vector2.Dot(rel, _right);

        if (s < 0.0f || s > Length || MathF.Abs(lateral) > Width * 0.5f)
        {
            return 0.0f;
        }

        return Height * (s / Length);
    }
}
