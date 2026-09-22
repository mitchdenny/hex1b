using System.Security.Cryptography;
using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// The environment a Sixel graphic is rasterized against.
/// </summary>
/// <param name="Background">The background captured when the graphic was created.</param>
/// <param name="Registers">The terminal-scoped color registers mutated in command order.</param>
/// <param name="Policy">The compatibility and resource policy.</param>
internal sealed record SixelRasterEnvironment(
    Rgba32 Background,
    SixelColorRegisters Registers,
    SixelCompatibilityPolicy Policy)
{
    /// <summary>
    /// Creates an environment with the deterministic default background and a
    /// fresh default palette. Used when no terminal state is available.
    /// </summary>
    public static SixelRasterEnvironment CreateDefault()
    {
        var policy = SixelCompatibilityPolicy.Default;
        return new SixelRasterEnvironment(
            policy.DefaultBackground,
            new SixelColorRegisters(policy),
            policy);
    }
}
