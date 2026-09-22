using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// A single explicit rasterization diagnostic explaining a geometry-only
/// downgrade or other annotated raster outcome.
/// </summary>
/// <param name="Code">The specific reason this diagnostic was raised.</param>
/// <param name="Message">A human-readable explanation.</param>
public readonly record struct SixelRasterDiagnostic(
    SixelRasterDiagnosticCode Code,
    string Message);
