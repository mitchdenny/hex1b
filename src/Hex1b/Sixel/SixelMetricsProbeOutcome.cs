namespace Hex1b.Sixel;

/// <summary>
/// The outcome of one attempt to obtain Sixel protocol cell metrics from a single
/// source during capability discovery.
/// </summary>
/// <remarks>
/// See <see cref="SixelCapabilityProbeDiagnostics"/> for the bounded, per-source
/// attempt log this enum feeds into.
/// </remarks>
public enum SixelMetricsProbeOutcome
{
    /// <summary>
    /// This source was never queried, typically because a higher-precedence source
    /// already produced a sufficient, authoritative answer.
    /// </summary>
    NotAttempted,

    /// <summary>
    /// The source responded with a plausible value that was accepted.
    /// </summary>
    Accepted,

    /// <summary>
    /// No response arrived within the bounded probe deadline.
    /// </summary>
    TimedOut,

    /// <summary>
    /// A response arrived but could not be parsed as the expected shape.
    /// </summary>
    Malformed,

    /// <summary>
    /// A response was parsed but rejected as implausible (zero, negative,
    /// non-finite, overflowing, or otherwise untrustworthy).
    /// </summary>
    Rejected,
}
