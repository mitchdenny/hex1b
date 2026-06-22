namespace DirtRaceDemo.Physics;

using Hex1b.Scene.Math;

/// <summary>
/// The physical world the vehicle drives through. Aggregates the track surface, jump ramps
/// and blocking obstacles behind a small query surface that the vehicle integrator uses.
/// </summary>
public interface IRaceWorld
{
    /// <summary>Surface height (Y) at an XZ point, combining track, bridge and ramps.</summary>
    float SurfaceHeightAt(float x, float z);

    /// <summary>True when the XZ point is on the graded track (full grip), false on dirt.</summary>
    bool IsOnTrack(float x, float z);

    /// <summary>
    /// Resolve a horizontal move against blocking obstacles, returning the allowed end point.
    /// Sets <paramref name="blocked"/> when the move was stopped by an obstacle.
    /// </summary>
    Vector2 ResolveCollision(Vector2 from, Vector2 to, float height, out bool blocked);
}
