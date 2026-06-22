namespace DirtRaceDemo.Physics;

using DirtRaceDemo.Track;
using Hex1b.Scene.Math;

/// <summary>
/// Concrete <see cref="IRaceWorld"/> that combines the figure-eight track surface, jump ramps
/// and blocking obstacles into the queries the vehicle integrator needs.
/// </summary>
public sealed class PhysicsWorld : IRaceWorld
{
    private readonly FigureEightTrack _track;
    private readonly IReadOnlyList<Ramp> _ramps;
    private readonly IReadOnlyList<Obstacle> _obstacles;

    public PhysicsWorld(FigureEightTrack track, IReadOnlyList<Ramp> ramps, IReadOnlyList<Obstacle> obstacles)
    {
        _track = track;
        _ramps = ramps;
        _obstacles = obstacles;
    }

    public float SurfaceHeightAt(float x, float z)
    {
        var height = _track.SurfaceHeightAt(new Vector2(x, z));
        foreach (var ramp in _ramps)
        {
            var rampHeight = ramp.HeightAt(x, z);
            if (rampHeight > height)
            {
                height = rampHeight;
            }
        }

        return height;
    }

    public bool IsOnTrack(float x, float z)
    {
        if (_track.IsOnTrack(new Vector2(x, z)))
        {
            return true;
        }

        foreach (var ramp in _ramps)
        {
            if (ramp.HeightAt(x, z) > 0.0f)
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 ResolveCollision(Vector2 from, Vector2 to, float height, out bool blocked)
    {
        foreach (var obstacle in _obstacles)
        {
            // Only block while the chassis is below the obstacle's top; clearing it by jumping
            // lets the truck sail over.
            if (height < obstacle.Height - 0.1f && obstacle.Contains(to))
            {
                blocked = true;
                return from;
            }
        }

        blocked = false;
        return to;
    }
}
