using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// The authoritative bounded rasterization of one Sixel sequence.
/// </summary>
internal sealed record SixelRasterResult(
    SixelRasterStatus Status,
    SixelRasterExtents Extents,
    SixelRasterImage? Image,
    SixelBackgroundMode BackgroundMode,
    Rgba32 UnpaintedPixel,
    byte[] Identity,
    IReadOnlyList<SixelRasterDiagnostic> Diagnostics);
