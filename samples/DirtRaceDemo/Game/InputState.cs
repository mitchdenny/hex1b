namespace DirtRaceDemo.Game;

using DirtRaceDemo.Physics;

/// <summary>
/// Translates discrete key presses into the continuous driver intent the physics needs.
///
/// Terminals do not report key-up events; holding a key produces a burst of key-repeat
/// presses after an initial delay. Each press tops an accumulator back up to 1.0 and the
/// accumulators decay every frame, so a held key reads as a sustained input while a tap fades
/// out smoothly.
/// </summary>
public sealed class InputState
{
    private const float Decay = 7.0f;

    private float _forward;
    private float _reverse;
    private float _left;
    private float _right;
    private float _handbrake;

    public bool ResetRequested { get; private set; }

    public void PressForward() => _forward = 1.0f;
    public void PressReverse() => _reverse = 1.0f;
    public void PressLeft() => _left = 1.0f;
    public void PressRight() => _right = 1.0f;
    public void PressHandbrake() => _handbrake = 1.0f;
    public void RequestReset() => ResetRequested = true;

    public void ClearResetRequest() => ResetRequested = false;

    public void Decayed(float dt)
    {
        var factor = MathF.Exp(-Decay * dt);
        _forward *= factor;
        _reverse *= factor;
        _left *= factor;
        _right *= factor;
        _handbrake *= factor;
    }

    public VehicleInput ToVehicleInput()
    {
        var throttle = Math.Clamp(_forward - _reverse, -1.0f, 1.0f);
        var steer = Math.Clamp(_right - _left, -1.0f, 1.0f);
        var handbrake = _handbrake > 0.3f;
        return new VehicleInput(throttle, steer, handbrake);
    }
}
