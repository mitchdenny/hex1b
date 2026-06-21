namespace DirtRaceDemo.Physics;

using Hex1b.Scene.Math;

/// <summary>
/// The arcade truck. Holds kinematic state on the XZ plane plus a vertical channel for jumps,
/// and integrates one fixed conceptual step per frame against an <see cref="IRaceWorld"/>.
///
/// Vertical motion is driven entirely by the world surface height: the truck follows the
/// ground while grounded, storing the rate at which the surface rises beneath it. When the
/// surface falls away faster than the truck can drop (a ramp lip or bridge crest) that stored
/// upward rate launches it into the air, where gravity takes over until it lands again.
/// </summary>
public sealed class ArcadeVehicle
{
    private readonly VehicleTuning _tuning;

    public Vector2 PositionXZ { get; private set; }
    public float Height { get; private set; }
    public float Heading { get; private set; }
    public Vector2 VelocityXZ { get; private set; }
    public float VerticalVelocity { get; private set; }
    public bool Grounded { get; private set; } = true;
    public float AirTime { get; private set; }

    public ArcadeVehicle(VehicleTuning tuning, Vector2 startPosition, float startHeading)
    {
        _tuning = tuning;
        Reset(startPosition, startHeading);
    }

    public Vector2 ForwardXZ => new(MathF.Sin(Heading), MathF.Cos(Heading));
    public Vector2 RightXZ => new(MathF.Cos(Heading), -MathF.Sin(Heading));

    /// <summary>Forward speed (positive forwards, negative in reverse) in world units / s.</summary>
    public float ForwardSpeed => Vector2.Dot(VelocityXZ, ForwardXZ);

    /// <summary>Scalar planar speed in world units / s.</summary>
    public float Speed => VelocityXZ.Magnitude;

    /// <summary>World-space position including the ride height of the chassis pivot.</summary>
    public Vector3 WorldPosition => new(PositionXZ.X, Height + _tuning.RideHeight, PositionXZ.Y);

    public void Reset(Vector2 position, float heading)
    {
        PositionXZ = position;
        Heading = heading;
        VelocityXZ = Vector2.Zero;
        VerticalVelocity = 0.0f;
        Height = 0.0f;
        Grounded = true;
        AirTime = 0.0f;
    }

    public void Step(float dt, VehicleInput input, IRaceWorld world)
    {
        var onTrack = world.IsOnTrack(PositionXZ.X, PositionXZ.Y);

        IntegrateSteering(dt, input);
        if (Grounded)
        {
            IntegrateDrive(dt, input, onTrack);
        }

        IntegrateHorizontal(dt, world);
        IntegrateVertical(dt, world);
    }

    private void IntegrateSteering(float dt, VehicleInput input)
    {
        var authority = MathF.Min(1.0f, Speed / _tuning.SteerFullSpeed);
        var directionSign = ForwardSpeed >= 0.0f ? 1.0f : -1.0f;
        Heading += input.Steer * _tuning.SteerRate * authority * directionSign * dt;
    }

    private void IntegrateDrive(float dt, VehicleInput input, bool onTrack)
    {
        VelocityXZ += ForwardXZ * (input.Throttle * _tuning.Acceleration * dt);

        // Split velocity into forward / lateral and bleed the lateral component to model grip.
        var forwardComponent = Vector2.Dot(VelocityXZ, ForwardXZ);
        var lateralComponent = Vector2.Dot(VelocityXZ, RightXZ);

        var grip = input.Handbrake
            ? _tuning.HandbrakeGrip
            : (onTrack ? _tuning.Grip : _tuning.DirtGrip);
        lateralComponent *= MathF.Exp(-grip * dt);

        VelocityXZ = ForwardXZ * forwardComponent + RightXZ * lateralComponent;

        // Rolling drag and a speed cap that depends on the surface.
        var drag = onTrack ? _tuning.Drag : _tuning.DirtDrag;
        VelocityXZ *= MathF.Exp(-drag * dt);

        var maxSpeed = onTrack ? _tuning.MaxSpeed : _tuning.DirtMaxSpeed;
        if (Speed > maxSpeed)
        {
            VelocityXZ = VelocityXZ.Normalized * maxSpeed;
        }
    }

    private void IntegrateHorizontal(float dt, IRaceWorld world)
    {
        var target = PositionXZ + VelocityXZ * dt;
        var resolved = world.ResolveCollision(PositionXZ, target, Height, out var blocked);
        if (blocked)
        {
            VelocityXZ *= 0.2f;
        }

        PositionXZ = resolved;
    }

    private void IntegrateVertical(float dt, IRaceWorld world)
    {
        var groundHeight = world.SurfaceHeightAt(PositionXZ.X, PositionXZ.Y);

        VerticalVelocity -= _tuning.Gravity * dt;
        var freeY = Height + VerticalVelocity * dt;

        if (freeY <= groundHeight)
        {
            // Stay on (or return to) the surface and carry any upward climb rate so that a
            // sudden drop at a ramp lip or bridge crest converts into a launch next step.
            var climbRate = (groundHeight - Height) / dt;
            Height = groundHeight;
            VerticalVelocity = MathF.Max(0.0f, climbRate);
            Grounded = true;
            AirTime = 0.0f;
        }
        else
        {
            Height = freeY;
            Grounded = false;
            AirTime += dt;
        }
    }
}
