using System.Runtime.CompilerServices;

namespace Hex1b;

/// <summary>
/// Backoff policy used by retrying transports such as
/// <see cref="Hmp1Transports.RetryingUnixSocket(string, RetryPolicy?)"/>.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>Initial delay before the second attempt.</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Maximum delay between attempts.</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Multiplier applied to the delay after each failed attempt.</summary>
    public double Multiplier { get; init; } = 1.5;

    /// <summary>
    /// Maximum number of attempts before giving up. Zero means infinite (the
    /// default) — retries continue until the supplied <see cref="CancellationToken"/>
    /// is cancelled.
    /// </summary>
    public int MaxAttempts { get; init; } = 0;

    /// <summary>
    /// Optional hook invoked after each failed attempt (before the delay).
    /// </summary>
    public Action<RetryAttemptFailedEventArgs>? OnAttemptFailed { get; init; }

    /// <summary>
    /// Reasonable defaults for waiting on a UDS producer to come online:
    /// 200 ms initial, 1.5× backoff, capped at 2 s, infinite attempts.
    /// </summary>
    public static RetryPolicy DefaultUnixSocket { get; } = new();
}
