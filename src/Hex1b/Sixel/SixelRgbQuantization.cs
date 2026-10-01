using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Identifies how 0-100 RGB components are converted to 8-bit values.
/// </summary>
internal enum SixelRgbQuantization
{
    /// <summary>Round to the nearest 8-bit component.</summary>
    Nearest,

    /// <summary>Truncate the scaled component toward zero.</summary>
    Truncate,
}
