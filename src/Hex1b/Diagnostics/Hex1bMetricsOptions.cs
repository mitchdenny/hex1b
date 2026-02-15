namespace Hex1b.Diagnostics;

/// <summary>
/// Options for configuring Hex1b metrics behavior.
/// </summary>
public class Hex1bMetricsOptions
{
    /// <summary>
    /// When <see langword="true"/>, per-node timing histograms are recorded for
    /// measure, arrange, render, and reconcile phases, tagged by hierarchical
    /// metric path. This is intended for local development with Aspire and
    /// should not be enabled in production due to high tag cardinality.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool EnablePerNodeMetrics { get; set; }
}
