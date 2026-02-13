namespace Hex1b.Animation;

/// <summary>
/// An animator that interpolates between two numeric values using eased progress.
/// Supported types are <see cref="int"/>, <see cref="float"/>, and <see cref="double"/>.
/// </summary>
/// <typeparam name="T">The numeric type (int, float, or double).</typeparam>
public class NumericAnimator<T> : Hex1bAnimator where T : struct
{
    private static readonly Func<double, T, T, T> _lerp = ResolveLerp();

    /// <summary>The starting value.</summary>
    public T From { get; set; }

    /// <summary>The ending value.</summary>
    public T To { get; set; }

    /// <summary>The current interpolated value based on eased progress.</summary>
    public T Value => _lerp(Progress, From, To);

    /// <summary>
    /// Retargets the animation toward a new value, starting from the current interpolated value.
    /// If already at the target, no animation is started.
    /// </summary>
    public void AnimateTo(T target)
    {
        if (EqualityComparer<T>.Default.Equals(To, target) && (IsRunning || IsCompleted))
            return;

        From = Value;
        To = target;
        Start();
    }

    private static Func<double, T, T, T> ResolveLerp()
    {
        if (typeof(T) == typeof(double))
            return (Func<double, T, T, T>)(object)(Func<double, double, double, double>)
                ((t, a, b) => a + (b - a) * t);

        if (typeof(T) == typeof(float))
            return (Func<double, T, T, T>)(object)(Func<double, float, float, float>)
                ((t, a, b) => (float)(a + (b - a) * t));

        if (typeof(T) == typeof(int))
            return (Func<double, T, T, T>)(object)(Func<double, int, int, int>)
                ((t, a, b) => (int)Math.Round(a + (b - a) * t));

        throw new NotSupportedException($"NumericAnimator does not support type {typeof(T).Name}. Use int, float, or double.");
    }
}

/// <summary>
/// Convenience animator for opacity values (0.0 to 1.0).
/// </summary>
public class OpacityAnimator : NumericAnimator<double>
{
    public OpacityAnimator()
    {
        From = 0.0;
        To = 1.0;
    }
}
