using Hex1b.Tokens;

namespace Hex1b.Sixel;

/// <summary>
/// A pixel aspect ratio expressed as a numerator/denominator pair (DECGRA/DECSIXEL macro).
/// </summary>
/// <param name="Numerator">The vertical scale numerator.</param>
/// <param name="Denominator">The vertical scale denominator.</param>
public readonly record struct SixelAspectRatio(int Numerator, int Denominator);
