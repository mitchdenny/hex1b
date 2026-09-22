using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// The separate extents preserved for downstream placement and translation.
/// </summary>
/// <param name="Logical">The unscaled canvas; six logical rows per Sixel band.</param>
/// <param name="Rendered">The logical canvas after applying the pixel aspect ratio.</param>
/// <param name="Declared">The DECGRA <c>Ph</c>/<c>Pv</c> hint, or empty when absent.</param>
/// <param name="Data">The unscaled extent reached by data commands.</param>
/// <param name="Painted">The unscaled bounds of explicitly painted pixels.</param>
/// <param name="Aspect">The effective pixel aspect ratio.</param>
public readonly record struct SixelRasterExtents(
    SixelExtent Logical,
    SixelExtent Rendered,
    SixelExtent Declared,
    SixelExtent Data,
    SixelBounds Painted,
    SixelAspectRatio Aspect)
{
    /// <summary>An empty set of extents, using the default 2:1 aspect ratio.</summary>
    public static SixelRasterExtents Empty { get; } = new(
        SixelExtent.Empty,
        SixelExtent.Empty,
        SixelExtent.Empty,
        SixelExtent.Empty,
        SixelBounds.Empty,
        new SixelAspectRatio(2, 1));
}
