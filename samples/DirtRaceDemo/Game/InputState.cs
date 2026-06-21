namespace DirtRaceDemo.Game;

using DirtRaceDemo.Physics;

/// <summary>
/// Translates discrete key presses into the continuous driver intent the physics needs.
///
/// Terminals do not report key-up events and cannot report two keys held at once, so throttle
/// uses a <em>cruise-control</em> model instead of "hold to accelerate": W nudges a persistent
/// cruise level up and S nudges it down (into reverse), and the truck keeps driving at whatever
/// level was last set. Steering still uses an impulse + decay accumulator so a single held
/// arrow / A / D key reads as a sustained turn while a tap fades out smoothly.
/// </summary>
public sealed class InputState
{
    private const float Decay = 7.0f;
    private const float CruiseStep = 0.12f;

    private float _cruise;
    private float _left;
    private float _right;
    private float _handbrake;

    public bool ResetRequested { get; private set; }

    /// <summary>The persistent throttle level in [-1, 1] (negative is reverse).</summary>
    public float Cruise => _cruise;

    public void PressForward() => _cruise = Math.Clamp(_cruise + CruiseStep, -1.0f, 1.0f);
    public void PressReverse() => _cruise = Math.Clamp(_cruise - CruiseStep, -1.0f, 1.0f);
    public void PressLeft() => _left = 1.0f;
    public void PressRight() => _right = 1.0f;
    public void PressHandbrake() => _handbrake = 1.0f;
    public void RequestReset() => ResetRequested = true;

    public void ClearResetRequest() => ResetRequested = false;

    /// <summary>Cuts the cruise level back to a standstill (used on reset).</summary>
    public void ResetCruise() => _cruise = 0.0f;

    public void Decayed(float dt)
    {
        var factor = MathF.Exp(-Decay * dt);
        _left *= factor;
        _right *= factor;
        _handbrake *= factor;
    }

    public VehicleInput ToVehicleInput()
    {
        var steer = Math.Clamp(_right - _left, -1.0f, 1.0f);
        var handbrake = _handbrake > 0.3f;
        return new VehicleInput(_cruise, steer, handbrake);
    }
}
