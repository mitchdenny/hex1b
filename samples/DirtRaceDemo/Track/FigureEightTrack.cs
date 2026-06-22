namespace DirtRaceDemo.Track;

using Hex1b.Scene.Math;

/// <summary>
/// A single point sampled along the track centreline. <see cref="Position"/> is on the
/// XZ ground plane (X = world X, Y = world Z); <see cref="Height"/> is the surface height
/// at that point (non-zero where the crossover bridge arches one branch over the other).
/// </summary>
public readonly record struct TrackSample(float T, Vector2 Position, float Height, Vector2 Tangent);

/// <summary>
/// A figure-eight track described by a lemniscate of Bernoulli. The two lobes cross at the
/// origin where a raised <em>crossover bridge</em> lifts one branch over the other. The class
/// owns the analytic centreline plus a cached set of samples used for surface-height,
/// on-track and lap-progress queries.
/// </summary>
public sealed class FigureEightTrack
{
    public float Scale { get; }
    public float HalfWidth { get; }

    private readonly TrackSample[] _samples;

    // Bridge: raise the branch passing the origin near t = PI/2 up and over the other.
    private const float BridgeCenterT = MathF.PI / 2.0f;
    private const float BridgeWindow = 0.55f;
    private const float BridgeHeight = 2.2f;

    public FigureEightTrack(float scale = 38.0f, float halfWidth = 2.6f, int sampleCount = 480)
    {
        Scale = scale;
        HalfWidth = halfWidth;

        _samples = new TrackSample[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (float)sampleCount * MathF.PI * 2.0f;
            var pos = CenterlineAt(t);
            var ahead = CenterlineAt(t + 0.01f);
            var tangent = (ahead - pos).Normalized;
            _samples[i] = new TrackSample(t, pos, HeightAt(t), tangent);
        }
    }

    public IReadOnlyList<TrackSample> Samples => _samples;

    /// <summary>The analytic centreline position (XZ) for parameter <paramref name="t"/>.</summary>
    public Vector2 CenterlineAt(float t)
    {
        var s = MathF.Sin(t);
        var c = MathF.Cos(t);
        var denom = 1.0f + s * s;
        return new Vector2(Scale * c / denom, Scale * s * c / denom);
    }

    /// <summary>Surface height of the centreline at parameter <paramref name="t"/> (bridge bump).</summary>
    public float HeightAt(float t)
    {
        var delta = AngleDelta(t, BridgeCenterT);
        var n = delta / BridgeWindow;
        if (MathF.Abs(n) >= 1.0f)
        {
            return 0.0f;
        }

        // Smooth raised cosine: 0 at the window edges, BridgeHeight at the centre.
        var bump = 0.5f * (1.0f + MathF.Cos(n * MathF.PI));
        return BridgeHeight * bump;
    }

    public TrackSample StartSample => _samples[0];

    public Vector2 StartPosition => _samples[0].Position;

    /// <summary>Heading angle (radians) tangent to the track at the start.</summary>
    public float StartHeading
    {
        get
        {
            var tan = _samples[0].Tangent;
            return MathF.Atan2(tan.X, tan.Y);
        }
    }

    /// <summary>Find the nearest centreline sample (and its array index) to an XZ point.</summary>
    public TrackSample NearestSample(Vector2 point, out int index)
    {
        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < _samples.Length; i++)
        {
            var d = (_samples[i].Position - point).SqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        index = best;
        return _samples[best];
    }

    public bool IsOnTrack(Vector2 point)
    {
        var sample = NearestSample(point, out _);
        return (sample.Position - point).Magnitude <= HalfWidth;
    }

    /// <summary>Surface height at an XZ point: the bridge deck height when on-track, else ground (0).</summary>
    public float SurfaceHeightAt(Vector2 point)
    {
        var sample = NearestSample(point, out _);
        if ((sample.Position - point).Magnitude > HalfWidth)
        {
            return 0.0f;
        }

        return sample.Height;
    }

    /// <summary>Normalised lap progress [0,1) at an XZ point (nearest sample parameter).</summary>
    public float Progress(Vector2 point)
    {
        NearestSample(point, out var index);
        return index / (float)_samples.Length;
    }

    private static float AngleDelta(float a, float b)
    {
        var d = a - b;
        while (d > MathF.PI) d -= MathF.PI * 2.0f;
        while (d < -MathF.PI) d += MathF.PI * 2.0f;
        return d;
    }
}
