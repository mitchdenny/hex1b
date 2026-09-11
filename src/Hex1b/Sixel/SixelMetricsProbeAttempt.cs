namespace Hex1b.Sixel;

/// <summary>
/// Records the outcome of one Sixel cell-metrics discovery attempt against a single
/// source (a specific query/response pair, or a local mechanism such as
/// <c>TIOCGWINSZ</c>).
/// </summary>
/// <param name="Source">Which source this attempt describes.</param>
/// <param name="Outcome">What happened when that source was consulted.</param>
/// <param name="Detail">
/// A short, human-readable explanation, most useful for <see cref="SixelMetricsProbeOutcome.Malformed"/>
/// or <see cref="SixelMetricsProbeOutcome.Rejected"/> outcomes (for example, which
/// dimension was implausible and why). May be <see langword="null"/> when the outcome
/// is self-explanatory.
/// </param>
public readonly record struct SixelMetricsProbeAttempt(
    SixelCellMetricsSource Source,
    SixelMetricsProbeOutcome Outcome,
    string? Detail = null);
