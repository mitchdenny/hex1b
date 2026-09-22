namespace Hex1b.Sixel;

/// <summary>
/// Identifies where a <see cref="SixelCellMetrics"/> value came from.
/// </summary>
/// <remarks>
/// Sixel cell metrics are protocol metrics, not physical font metrics. Windows
/// Terminal, for example, deliberately exposes a VT-compatible virtual Sixel grid
/// (commonly 9x20 or 10x20) instead of its physical glyph box, so the source must
/// travel with the value.
/// </remarks>
public enum SixelCellMetricsSource
{
    /// <summary>
    /// The presentation reported protocol cell metrics directly.
    /// </summary>
    Direct,

    /// <summary>
    /// The metrics came from an XTWINOPS <c>CSI 16 t</c> report.
    /// </summary>
    Csi16,

    /// <summary>
    /// The metrics came from an OSC 1337 report.
    /// </summary>
    Osc1337,

    /// <summary>
    /// The metrics were computed from other reported geometry, such as a window
    /// pixel size divided by the character grid.
    /// </summary>
    Derived,

    /// <summary>
    /// No report was available and a documented default was assumed.
    /// </summary>
    Assumed,
}
