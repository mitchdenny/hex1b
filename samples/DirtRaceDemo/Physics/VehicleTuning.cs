namespace DirtRaceDemo.Physics;

/// <summary>
/// Tunable constants that define the arcade feel of the 4x4. Kept in one place so the
/// handling can be adjusted without touching the integration code.
/// </summary>
public sealed class VehicleTuning
{
    /// <summary>Forward acceleration applied at full throttle (world units / s^2).</summary>
    public float Acceleration { get; init; } = 26.0f;

    /// <summary>Top speed while on the graded track surface.</summary>
    public float MaxSpeed { get; init; } = 17.0f;

    /// <summary>Top speed while off-track on loose dirt.</summary>
    public float DirtMaxSpeed { get; init; } = 9.0f;

    /// <summary>Steering rate (radians / s) at full steering authority.</summary>
    public float SteerRate { get; init; } = 2.6f;

    /// <summary>Speed at which the vehicle reaches full steering authority.</summary>
    public float SteerFullSpeed { get; init; } = 6.0f;

    /// <summary>Exponential rolling-drag coefficient on the track.</summary>
    public float Drag { get; init; } = 0.7f;

    /// <summary>Exponential rolling-drag coefficient on dirt.</summary>
    public float DirtDrag { get; init; } = 2.4f;

    /// <summary>Lateral grip on the track (higher = sticks to its heading).</summary>
    public float Grip { get; init; } = 7.5f;

    /// <summary>Lateral grip on dirt (lower = slides more).</summary>
    public float DirtGrip { get; init; } = 3.0f;

    /// <summary>Lateral grip while the handbrake is held (near zero = full drift).</summary>
    public float HandbrakeGrip { get; init; } = 0.6f;

    /// <summary>Gravity used for jumps and falls.</summary>
    public float Gravity { get; init; } = 26.0f;

    /// <summary>Ride height of the chassis pivot above the contact surface.</summary>
    public float RideHeight { get; init; } = 0.45f;
}
