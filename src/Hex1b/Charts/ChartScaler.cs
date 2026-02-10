namespace Hex1b.Charts;

/// <summary>
/// Maps data values to cell positions within a chart's available space.
/// </summary>
/// <remarks>
/// <para>
/// Handles auto-scaling (deriving min/max from data) and manual range overrides.
/// Used by chart nodes to convert data values into pixel/cell coordinates.
/// </para>
/// </remarks>
internal sealed class ChartScaler
{
    /// <summary>
    /// Gets the minimum data value in the range.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the maximum data value in the range.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets the available space in cells for rendering.
    /// </summary>
    public int AvailableCells { get; }

    /// <summary>
    /// Creates a new scaler with the specified range and available space.
    /// </summary>
    /// <param name="minimum">The minimum data value.</param>
    /// <param name="maximum">The maximum data value.</param>
    /// <param name="availableCells">The number of cells available for rendering.</param>
    public ChartScaler(double minimum, double maximum, int availableCells)
    {
        Minimum = minimum;
        Maximum = maximum;
        AvailableCells = availableCells;
    }

    /// <summary>
    /// Creates a scaler that auto-derives the range from a set of values.
    /// </summary>
    /// <param name="values">The data values to derive the range from.</param>
    /// <param name="availableCells">The number of cells available for rendering.</param>
    /// <param name="explicitMin">Optional explicit minimum override.</param>
    /// <param name="explicitMax">Optional explicit maximum override.</param>
    /// <returns>A new <see cref="ChartScaler"/> with the computed range.</returns>
    public static ChartScaler FromValues(
        IEnumerable<double> values,
        int availableCells,
        double? explicitMin = null,
        double? explicitMax = null)
    {
        var min = explicitMin ?? 0.0;
        var max = explicitMax ?? 0.0;

        bool hasValues = false;
        foreach (var v in values)
        {
            if (!hasValues && !explicitMax.HasValue)
            {
                max = v;
            }

            if (!explicitMin.HasValue && v < min) min = v;
            if (!explicitMax.HasValue && v > max) max = v;
            hasValues = true;
        }

        // Ensure max > min to avoid division by zero
        if (max <= min)
        {
            max = min + 1.0;
        }

        return new ChartScaler(min, max, availableCells);
    }

    /// <summary>
    /// Scales a data value to a fractional cell position (0.0 to AvailableCells).
    /// </summary>
    /// <param name="value">The data value to scale.</param>
    /// <returns>The fractional cell position.</returns>
    public double Scale(double value)
    {
        if (Maximum <= Minimum) return 0.0;

        var normalized = (value - Minimum) / (Maximum - Minimum);
        return Math.Clamp(normalized * AvailableCells, 0.0, AvailableCells);
    }

    /// <summary>
    /// Scales a data value to a whole number of cells (rounded).
    /// </summary>
    /// <param name="value">The data value to scale.</param>
    /// <returns>The number of whole cells.</returns>
    public int ScaleToWholeCells(double value)
    {
        return (int)Math.Round(Scale(value));
    }
}
