namespace DirtRaceDemo.Game;

using DirtRaceDemo.Physics;
using Hex1b.Scene.Math;

/// <summary>A single recorded snapshot of the vehicle at a point in a lap.</summary>
public readonly record struct LapSample(float Time, Vector2 Position, float Height, float Heading);

/// <summary>
/// Accumulates <see cref="LapSample"/> snapshots while a lap is driven. Sampling is throttled to a
/// fixed interval so a long lap doesn't grow the buffer without bound; <see cref="Build"/> freezes
/// the captured path into an immutable <see cref="LapRecording"/> for ghost playback.
/// </summary>
public sealed class LapRecorder
{
    private const float SampleInterval = 1.0f / 30.0f;

    private readonly List<LapSample> _samples = new();
    private float _lastSampleTime = float.NegativeInfinity;

    public void Reset()
    {
        _samples.Clear();
        _lastSampleTime = float.NegativeInfinity;
    }

    public void Record(float lapTime, ArcadeVehicle vehicle)
    {
        if (_samples.Count > 0 && lapTime - _lastSampleTime < SampleInterval)
        {
            return;
        }

        _samples.Add(new LapSample(lapTime, vehicle.PositionXZ, vehicle.Height, vehicle.Heading));
        _lastSampleTime = lapTime;
    }

    public bool HasSamples => _samples.Count >= 2;

    public LapRecording Build() => new(_samples.ToArray());
}

/// <summary>
/// An immutable recording of a completed lap. <see cref="Sample"/> reconstructs the vehicle pose at
/// any lap time by interpolating between captured snapshots so a ghost can replay the line smoothly.
/// </summary>
public sealed class LapRecording
{
    private readonly LapSample[] _samples;

    public LapRecording(LapSample[] samples)
    {
        _samples = samples;
        Duration = samples.Length > 0 ? samples[^1].Time : 0.0f;
    }

    public float Duration { get; }

    /// <summary>
    /// Returns the interpolated pose at lap time <paramref name="time"/>. The time is clamped to the
    /// recording, so before the start it holds the first pose and after the end it holds the last.
    /// </summary>
    public LapSample Sample(float time)
    {
        if (_samples.Length == 1)
        {
            return _samples[0];
        }

        if (time <= _samples[0].Time)
        {
            return _samples[0];
        }

        if (time >= _samples[^1].Time)
        {
            return _samples[^1];
        }

        // Linear scan is fine: ghost playback advances monotonically and lap buffers are small.
        var index = 1;
        while (index < _samples.Length && _samples[index].Time < time)
        {
            index++;
        }

        var a = _samples[index - 1];
        var b = _samples[index];
        var span = b.Time - a.Time;
        var t = span > 1e-5f ? (time - a.Time) / span : 0.0f;

        return new LapSample(
            time,
            Vector2.Lerp(a.Position, b.Position, t),
            a.Height + (b.Height - a.Height) * t,
            LerpAngle(a.Heading, b.Heading, t));
    }

    private static float LerpAngle(float a, float b, float t)
    {
        var delta = b - a;
        while (delta > MathF.PI)
        {
            delta -= MathF.Tau;
        }

        while (delta < -MathF.PI)
        {
            delta += MathF.Tau;
        }

        return a + delta * t;
    }
}
