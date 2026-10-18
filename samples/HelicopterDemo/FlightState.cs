using System.Numerics;

namespace HelicopterDemo;

internal sealed class FlightState
{
    public const float WorldLimit = 36;
    public const float MaximumSpeed = 10;
    public const float DefaultRotorSpeed = 25;
    public const float MaximumRotorSpeed = 75;
    public const float InitialAltitude = 2.4f;
    public const float MaximumAltitude = 14;
    public const float MaximumTiltAngle = 0.35f;
    private const float Drag = 1.6f;
    private const float VerticalDrag = 1.4f;
    private const float Gravity = 9.81f;
    private const float MaximumVerticalSpeed = 6;
    private bool _paused;

    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public Vector2 Tilt { get; private set; }
    public int RotationDirection { get; private set; }
    public float Heading { get; private set; }
    public float RotorAngle { get; private set; }
    public float RotorSpeed { get; private set; } = DefaultRotorSpeed;
    public float Altitude { get; private set; } = InitialAltitude;
    public float VerticalVelocity { get; private set; }
    public bool IsGrounded => Altitude == 0;
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            if (value)
                ReleaseControls();
        }
    }

    public void AimAt(int x, int y, int width, int height)
    {
        if (Paused)
            return;
        if (width <= 1 || height <= 1 || x < 0 || y < 0 || x >= width || y >= height)
        {
            Tilt = Vector2.Zero;
            return;
        }

        var offset = new Vector2(2f * x / (width - 1) - 1, 2f * y / (height - 1) - 1);
        var distance = offset.Length();
        const float deadZone = 0.08f;
        Tilt = distance <= deadZone ? Vector2.Zero
            : offset / distance * ((Math.Min(1, distance) - deadZone) / (1 - deadZone));
    }

    public void SetRotationDirection(int direction)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(direction, -1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(direction, 1);
        if (!Paused)
            RotationDirection = direction;
    }

    public void ChangeRotorSpeed(int steps)
    {
        if (!Paused)
            RotorSpeed = Math.Clamp(RotorSpeed + steps * (DefaultRotorSpeed / 10), 0, MaximumRotorSpeed);
    }

    public void ReleaseControls()
    {
        Tilt = Vector2.Zero;
        RotationDirection = 0;
    }

    public void Nudge(Vector2 direction)
    {
        if (Paused || IsGrounded)
            return;
        Tilt = Vector2.Zero;
        Velocity += direction * 3;
        if (Velocity.Length() > MaximumSpeed)
            Velocity = Vector2.Normalize(Velocity) * MaximumSpeed;
    }

    public void Turn(float radians)
    {
        if (Paused)
            return;
        Heading = (Heading + radians) % MathF.Tau;
        if (Heading < 0)
            Heading += MathF.Tau;
    }

    public void Brake()
    {
        ReleaseControls();
        Velocity = Vector2.Zero;
    }

    public void Step(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (Paused)
            return;

        StepAltitude(seconds);
        if (IsGrounded)
            Velocity = Vector2.Zero;
        var damping = MathF.Exp(-Drag * seconds);
        var targetVelocity = IsGrounded ? Vector2.Zero : Tilt * MaximumSpeed;
        var next = Position + targetVelocity * seconds + (Velocity - targetVelocity) * ((1 - damping) / Drag);
        Position = Vector2.Clamp(next, new Vector2(-WorldLimit), new Vector2(WorldLimit));
        Velocity = targetVelocity + (Velocity - targetVelocity) * damping;
        if (next.X != Position.X)
            Velocity = new Vector2(0, Velocity.Y);
        if (next.Y != Position.Y)
            Velocity = new Vector2(Velocity.X, 0);
        Turn(RotationDirection * 1.8f * seconds);
        RotorAngle = (RotorAngle + seconds * RotorSpeed) % MathF.Tau;
    }

    private void StepAltitude(float seconds)
    {
        var rotorRatio = RotorSpeed / DefaultRotorSpeed;
        // Banking redirects rotor thrust sideways; only its upward component supports weight.
        var upwardFraction = MathF.Cos(Tilt.X * MaximumTiltAngle) * MathF.Cos(Tilt.Y * MaximumTiltAngle);
        var acceleration = Gravity * (rotorRatio * rotorRatio * upwardFraction - 1);
        var terminalVelocity = Math.Clamp(acceleration / VerticalDrag, -MaximumVerticalSpeed, MaximumVerticalSpeed);
        var damping = MathF.Exp(-VerticalDrag * seconds);
        var next = Altitude + terminalVelocity * seconds
            + (VerticalVelocity - terminalVelocity) * ((1 - damping) / VerticalDrag);
        Altitude = Math.Clamp(next, 0, MaximumAltitude);
        VerticalVelocity = terminalVelocity + (VerticalVelocity - terminalVelocity) * damping;
        if (next <= 0 && VerticalVelocity < 0 || next >= MaximumAltitude && VerticalVelocity > 0)
            VerticalVelocity = 0;
    }

    public void Reset()
    {
        Position = Vector2.Zero;
        Velocity = Vector2.Zero;
        ReleaseControls();
        Heading = 0;
        RotorAngle = 0;
        RotorSpeed = DefaultRotorSpeed;
        Altitude = InitialAltitude;
        VerticalVelocity = 0;
        Paused = false;
    }
}
