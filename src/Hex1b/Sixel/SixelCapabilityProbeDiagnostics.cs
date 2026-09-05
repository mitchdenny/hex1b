namespace Hex1b.Sixel;

/// <summary>
/// Bounded diagnostics describing how a presentation adapter discovered Sixel support
/// and protocol cell metrics.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a plain data snapshot, not a live log: the probe that
/// produces it runs once per adapter lifetime (see the owning adapter's
/// documentation for its exact invalidation rules), and this type records only its
/// final, bounded outcome — one <see cref="SixelMetricsProbeAttempt"/> per source
/// considered, never an unbounded stream of raw bytes.
/// </para>
/// <para>
/// <see cref="Attempts"/> is ordered by discovery precedence (highest-precedence
/// source first), matching the order in which sources are consulted.
/// </para>
/// </remarks>
/// <param name="Attempts">
/// One entry per source considered during discovery, in precedence order.
/// </param>
/// <param name="Da1DeclaresSixel">
/// Whether a Primary Device Attributes (DA1) reply was received and whether it
/// declared Sixel support (DEC parameter 4). <see langword="null"/> when DA1 was not
/// probed or timed out/was malformed — <see langword="null"/> is deliberately distinct
/// from <see langword="false"/> so "unknown" is never confused with "confirmed
/// unsupported."
/// </param>
/// <param name="SelectedMetrics">
/// The metrics ultimately selected after applying discovery precedence, or
/// <see langword="null"/> if no source produced an accepted value.
/// </param>
/// <param name="MetricsDisagreement">
/// Whether two or more accepted sources reported meaningfully different cell
/// dimensions. The higher-precedence value still wins per the documented discovery
/// order; this flag exists purely so callers can surface a diagnostic instead of the
/// disagreement passing by silently.
/// </param>
/// <param name="DisagreementDetail">
/// A short, human-readable summary of the disagreement when
/// <see cref="MetricsDisagreement"/> is <see langword="true"/>; otherwise
/// <see langword="null"/>.
/// </param>
public sealed record SixelCapabilityProbeDiagnostics(
    IReadOnlyList<SixelMetricsProbeAttempt> Attempts,
    bool? Da1DeclaresSixel,
    SixelCellMetrics? SelectedMetrics,
    bool MetricsDisagreement,
    string? DisagreementDetail)
{
    /// <summary>
    /// A diagnostics value representing "discovery has not run yet."
    /// </summary>
    public static SixelCapabilityProbeDiagnostics NotProbed { get; } = new(
        Array.Empty<SixelMetricsProbeAttempt>(),
        Da1DeclaresSixel: null,
        SelectedMetrics: null,
        MetricsDisagreement: false,
        DisagreementDetail: null);
}
