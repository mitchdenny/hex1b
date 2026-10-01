using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Captures the immutable inputs needed to rasterize a Sixel graphic lazily.
/// </summary>
/// <param name="Environment">
/// A private register snapshot and the background captured when the graphic was created.
/// </param>
/// <param name="Identity">
/// A deterministic identity for the captured background, palette, and policy.
/// </param>
internal sealed record SixelRasterPreparation(
    SixelRasterEnvironment Environment,
    byte[] Identity);
