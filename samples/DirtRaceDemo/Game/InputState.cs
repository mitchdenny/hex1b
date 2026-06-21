namespace DirtRaceDemo.Game;

using DirtRaceDemo.Physics;

/// <summary>
/// Translates discrete key presses into the continuous driver intent the physics needs.
///
/// Terminals do not report key-up events and cannot report two keys held at once, so both throttle
/// and steering use a <em>rate-control</em> model rather than "hold to act". W nudges a persistent
/// cruise level up and S nudges it down (into reverse); A and D nudge a steering <em>rate</em> left
/// and right. The throttle holds at whatever level was last dialled in, while the steering rate
/// eases back to centre over time (see <see cref="SteerReturnPerSecond"/>) so the driver feeds in a
/// turn to follow a line through a corner and the wheel self-straightens as they ease off. The
/// handbrake is the one momentary control and still uses an impulse + decay so a tap fades out.
/// </summary>
public sealed class InputState
{
    private const float Decay = 7.0f;
    private const float CruiseStep = 0.12f;
    private const float SteerStep = 0.2f;

    /// <summary>
    /// How quickly the steering rate eases back to centre, in units/second of exponential decay.
    /// Higher = the wheel straightens faster when you stop steering; 0 = fully persistent. Tune
    /// this until the cornering feel is right.
    /// </summary>
    private const float SteerReturnPerSecond = 1.6f;

    private float _cruise;
    private float _steer;
    private float _handbrake;

    public bool ResetRequested { get; private set; }

    /// <summary>The persistent throttle level in [-1, 1] (negative is reverse).</summary>
    public float Cruise => _cruise;

    /// <summary>The persistent steering level in [-1, 1] (negative is left, positive is right).</summary>
    public float Steer => _steer;

    public void PressForward() => _cruise = Math.Clamp(_cruise + CruiseStep, -1.0f, 1.0f);
    public void PressReverse() => _cruise = Math.Clamp(_cruise - CruiseStep, -1.0f, 1.0f);
    public void PressLeft() => _steer = Math.Clamp(_steer - SteerStep, -1.0f, 1.0f);
    public void PressRight() => _steer = Math.Clamp(_steer + SteerStep, -1.0f, 1.0f);
    public void PressHandbrake() => _handbrake = 1.0f;
    public void RequestReset() => ResetRequested = true;

    public void ClearResetRequest() => ResetRequested = false;

    /// <summary>Cuts the cruise level back to a standstill (used on reset).</summary>
    public void ResetCruise() => _cruise = 0.0f;

    /// <summary>Centres the steering level (used on reset).</summary>
    public void ResetSteer() => _steer = 0.0f;

    public void Decayed(float dt)
    {
        var factor = MathF.Exp(-Decay * dt);
        _handbrake *= factor;
        _steer *= MathF.Exp(-SteerReturnPerSecond * dt);
    }

    public VehicleInput ToVehicleInput()
    {
        var handbrake = _handbrake > 0.3f;
        return new VehicleInput(_cruise, _steer, handbrake);
    }
}
