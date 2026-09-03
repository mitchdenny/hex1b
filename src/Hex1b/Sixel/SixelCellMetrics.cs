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
internal enum SixelCellMetricsSource
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

/// <summary>
/// Describes how much a <see cref="SixelCellMetrics"/> value can be trusted.
/// </summary>
internal enum SixelCellMetricsReliability
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

/// <summary>
/// The protocol cell metrics a Sixel placement is measured against.
/// </summary>
/// <param name="Width">The protocol cell width in pixels. May be fractional.</param>
/// <param name="Height">The protocol cell height in pixels. May be fractional.</param>
/// <param name="Source">Where the metrics came from.</param>
/// <param name="Reliability">How much the metrics can be trusted.</param>
/// <remarks>
/// <para>
/// Metrics are captured once, when a completed Sixel sequence creates a placement,
/// so a later metric change cannot retroactively alter an existing placement's
/// recorded occupancy. Discovering real upstream metrics is owned by
/// <see href="https://github.com/mitchdenny/hex1b/issues/455">#455</see>; this type
/// only models the value and lets tests and adapters inject one.
/// </para>
/// <para>
/// Occupancy always uses ceiling division of the <em>rendered</em> (aspect-scaled)
/// pixel extent, so a graphic one pixel past a cell boundary occupies the whole
/// next cell.
/// </para>
/// </remarks>
internal readonly record struct SixelCellMetrics(
    double Width,
    double Height,
    SixelCellMetricsSource Source,
    SixelCellMetricsReliability Reliability)
{
    /// <summary>
    /// The documented fallback used when nothing is known about the presentation.
    /// </summary>
    public static SixelCellMetrics Unknown { get; } = new(
        10,
        20,
        SixelCellMetricsSource.Assumed,
        SixelCellMetricsReliability.Estimated);

    /// <summary>
    /// Gets a value indicating whether the metrics were reported by the upstream
    /// presentation for the Sixel protocol grid.
    /// </summary>
    public bool IsAuthoritative => Reliability == SixelCellMetricsReliability.Authoritative;

    /// <summary>
    /// Gets the sanitized cell width. Non-positive or non-finite widths fall back
    /// to <see cref="Unknown"/> so occupancy math stays deterministic.
    /// </summary>
    public double SafeWidth => Sanitize(Width, Unknown.Width);

    /// <summary>
    /// Gets the sanitized cell height.
    /// </summary>
    public double SafeHeight => Sanitize(Height, Unknown.Height);

    /// <summary>
    /// Creates metrics derived from <see cref="TerminalCapabilities"/>.
    /// </summary>
    /// <remarks>
    /// Capability metrics describe the presentation's text cell, not a negotiated
    /// Sixel protocol grid, so the result is never authoritative.
    /// </remarks>
    public static SixelCellMetrics FromCapabilities(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        return new SixelCellMetrics(
            capabilities.EffectiveCellPixelWidth,
            capabilities.CellPixelHeight,
            SixelCellMetricsSource.Derived,
            SixelCellMetricsReliability.Estimated);
    }

    /// <summary>
    /// Computes the number of columns a rendered pixel width occupies.
    /// </summary>
    /// <param name="renderedPixelWidth">The aspect-scaled horizontal extent.</param>
    public int ColumnsFor(int renderedPixelWidth) => Occupancy(renderedPixelWidth, SafeWidth);

    /// <summary>
    /// Computes the number of rows a rendered pixel height occupies.
    /// </summary>
    /// <param name="renderedPixelHeight">The aspect-scaled vertical extent.</param>
    public int RowsFor(int renderedPixelHeight) => Occupancy(renderedPixelHeight, SafeHeight);

    /// <summary>
    /// Formats the metrics for diagnostics, keeping estimated values visibly
    /// distinct from authoritative ones.
    /// </summary>
    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Width:0.###}x{Height:0.###}px ({Source}/{Reliability})");

    private static int Occupancy(int renderedPixels, double cellPixels)
    {
        if (renderedPixels <= 0)
        {
            return 0;
        }

        var cells = Math.Ceiling(renderedPixels / cellPixels);
        return cells >= int.MaxValue ? int.MaxValue : (int)cells;
    }

    private static double Sanitize(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;
}
