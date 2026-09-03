using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// Describes the rectangle a scroll or margin operation affects, for the
/// purpose of deciding which Sixel placements shift, get clipped, or drop.
/// </summary>
/// <remarks>
/// Deliberately independent of <see cref="KgpScrollRectangle"/>: the two
/// graphics states share the general shape of this concept, but keeping the
/// types separate avoids any compile-time coupling between the Sixel and KGP
/// graphics models.
/// </remarks>
internal readonly record struct SixelScrollRegion(int Top, int Bottom, int Left, int Right);
