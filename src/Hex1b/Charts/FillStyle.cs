namespace Hex1b.Charts;

/// <summary>
/// Controls the area fill style for time series charts.
/// </summary>
public enum FillStyle
{
    /// <summary>
    /// No area fill — line only.
    /// </summary>
    None,

    /// <summary>
    /// Solid fill using vertical block characters (▁▂▃▄▅▆▇█) below the line.
    /// </summary>
    Solid,

    /// <summary>
    /// Braille dot fill below the line for a lighter appearance.
    /// </summary>
    Braille
}
