using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Explicit reasons a rasterization degraded or annotated its result,
/// surfaced via <see cref="SixelRasterDiagnostic"/> on
/// <see cref="Hex1b.SixelData.RasterDiagnostics"/>.
/// </summary>
public enum SixelRasterDiagnosticCode
{
    /// <summary>The parse outcome (cancelled/malformed/rejected) carried no complete graphic to rasterize.</summary>
    ParseOutcomeNotRasterable,

    /// <summary>Bounded command retention truncated the sequence, leaving only geometry and palette state.</summary>
    CommandsIncomplete,

    /// <summary>The sequence produced no logical raster extent.</summary>
    NoRasterableExtent,

    /// <summary>The logical raster extent exceeded the implementation coordinate limit.</summary>
    RasterExtentOverflow,

    /// <summary>The logical pixel count exceeded the configured raster pixel limit.</summary>
    RasterPixelLimitExceeded,

    /// <summary>The number of requested pixel writes exceeded the configured raster operation limit.</summary>
    RasterOperationLimitExceeded,

    /// <summary>A tiled-raster resource limit was exceeded.</summary>
    RasterTileLimitExceeded,

    /// <summary>A referenced color register fell outside the compatibility policy's accepted range.</summary>
    ColorRegisterOutOfPolicy,
}
