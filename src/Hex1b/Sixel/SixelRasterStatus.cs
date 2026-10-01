using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Describes whether a rasterization produced pixels or only geometry.
/// </summary>
/// <remarks>
/// A <see cref="GeometryOnly"/> outcome means the authoritative rasterizer
/// explicitly refused pixel allocation (a bounded resource limit, or a parse
/// outcome that carried no rasterable data), not a bug: the placement is
/// still retained with its declared geometry and explanatory
/// <see cref="Hex1b.SixelData.RasterDiagnostics"/>, never silently dropped.
/// </remarks>
public enum SixelRasterStatus
{
    /// <summary>Pixels are available through <see cref="Hex1b.SixelData.GetPixels"/>.</summary>
    Rasterized,

    /// <summary>
    /// Allocation was explicitly refused or the payload carried no rasterable
    /// data. Geometry and diagnostics remain available.
    /// </summary>
    GeometryOnly,
}
