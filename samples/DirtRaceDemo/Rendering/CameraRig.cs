namespace DirtRaceDemo.Rendering;

using Hex1b.Scene.Math;
using Hex1b.Scene.Objects;

/// <summary>
/// A chase camera that sits behind and above the truck looking down at it. The truck is kept at
/// the world origin (the scene is translated under it by <c>RaceGame</c>), so the rig only needs a
/// yaw: it orbits to whatever heading it is given and looks just ahead of the origin. The yaw is
/// spring-smoothed by the caller so the camera swings in behind the truck gently rather than
/// snapping, giving a classic arcade chase feel.
/// </summary>
public sealed class CameraRig
{
    private readonly float _horizontalDistance;
    private readonly float _height;
    private readonly float _lookAhead;

    public SceneIsometricCamera Camera { get; }

    public CameraRig(
        float size = 11.0f,
        float horizontalDistance = 30.0f,
        float height = 44.0f,
        float lookAhead = 7.0f)
    {
        _horizontalDistance = horizontalDistance;
        _height = height;
        _lookAhead = lookAhead;

        Camera = new SceneIsometricCamera("RaceCamera")
        {
            Size = size,
            Far = 600.0f,
        };
    }

    /// <summary>
    /// Positions the camera behind a truck facing <paramref name="yaw"/> (the truck's heading),
    /// elevated and looking at a point a little ahead of the origin so the truck sits low in frame
    /// with the road ahead visible.
    /// </summary>
    public void SetYaw(float yaw)
    {
        var forwardX = MathF.Sin(yaw);
        var forwardZ = MathF.Cos(yaw);

        var target = new Vector3(forwardX * _lookAhead, 0.0f, forwardZ * _lookAhead);
        var position = new Vector3(
            target.X - forwardX * _horizontalDistance,
            _height,
            target.Z - forwardZ * _horizontalDistance);

        Camera.Position = position;
        Camera.Rotation = LookAtRotation(position, target);
    }

    private static Quaternion LookAtRotation(Vector3 from, Vector3 target)
    {
        var direction = (target - from).Normalized;
        var yaw = MathF.Atan2(-direction.X, -direction.Z);
        var pitch = MathF.Asin(direction.Y);
        return Quaternion.FromEulerAngles(pitch, yaw, 0.0f);
    }
}
