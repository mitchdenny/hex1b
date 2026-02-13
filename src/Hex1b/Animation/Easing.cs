namespace Hex1b.Animation;

/// <summary>
/// Standard easing functions for animations. Each function maps a linear
/// progress value (0..1) to an eased value (0..1).
/// </summary>
public static class Easing
{
    /// <summary>Linear interpolation (no easing).</summary>
    public static readonly Func<double, double> Linear = t => t;

    /// <summary>Quadratic ease-in (slow start).</summary>
    public static readonly Func<double, double> EaseInQuad = t => t * t;

    /// <summary>Quadratic ease-out (slow end).</summary>
    public static readonly Func<double, double> EaseOutQuad = t => t * (2 - t);

    /// <summary>Quadratic ease-in-out (slow start and end).</summary>
    public static readonly Func<double, double> EaseInOutQuad = t =>
        t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;

    /// <summary>Cubic ease-in (slow start).</summary>
    public static readonly Func<double, double> EaseInCubic = t => t * t * t;

    /// <summary>Cubic ease-out (slow end).</summary>
    public static readonly Func<double, double> EaseOutCubic = t =>
    {
        var t1 = t - 1;
        return t1 * t1 * t1 + 1;
    };

    /// <summary>Cubic ease-in-out (slow start and end).</summary>
    public static readonly Func<double, double> EaseInOutCubic = t =>
        t < 0.5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
}
