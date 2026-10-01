namespace Hex1b.Sixel;

/// <summary>
/// Describes how much a <see cref="SixelCellMetrics"/> value can be trusted.
/// </summary>
public enum SixelCellMetricsReliability
{
    /// <summary>
    /// The upstream presentation reported the value for the Sixel protocol grid.
    /// </summary>
    Authoritative,

    /// <summary>
    /// The value was computed from an authoritative report of different geometry.
    /// </summary>
    Derived,

    /// <summary>
    /// The value is a guess and may not match the upstream presentation.
    /// </summary>
    Estimated,
}
